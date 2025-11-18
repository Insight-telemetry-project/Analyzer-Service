using System;
using System.Collections.Generic;
using System.Linq;
using Analyzer_Service.Models.Interface.Algorithms.Ccm;
using Analyzer_Service.Models.Algorithms.Ccm;

namespace Analyzer_Service.Services.Algorithms.Ccm
{
    public class CcmCausalityAnalyzer : ICcmCausalityAnalyzer
    {
        public double ComputeCausality(
            List<double> sourceSeries,
            List<double> targetSeries,
            int embeddingDimension,
            int timeDelay)
        {
            int totalSamples = sourceSeries.Count;
            int requiredSamples = (embeddingDimension - 1) * timeDelay + 1;

  
            List<double> normalizedSource = NormalizeZScore(sourceSeries);
            List<double> normalizedTarget = NormalizeZScore(targetSeries);

            EmbeddingResult sourceEmbedding =
                BuildTakensEmbedding(normalizedSource, embeddingDimension, timeDelay);

            EmbeddingResult targetEmbedding =
                BuildTakensEmbedding(normalizedTarget, embeddingDimension, timeDelay);

            int totalEmbeddingVectors =
                Math.Min(sourceEmbedding.Vectors.Count, targetEmbedding.Vectors.Count);

         

            int minLibrarySize = Math.Max(20, embeddingDimension + 2);
            int maxLibrarySize = Math.Min(500, totalEmbeddingVectors - 5);
            int libraryStep = Math.Max(10, (maxLibrarySize - minLibrarySize) / 10);

            double bestCorrelation = 0.0;

            for (int currentLibrarySize = minLibrarySize;
                 currentLibrarySize <= maxLibrarySize;
                 currentLibrarySize += libraryStep)
            {
                double correlation = ComputeCorrelationForLibrary(
                    normalizedSource,
                    targetEmbedding,
                    sourceEmbedding,
                    currentLibrarySize,
                    embeddingDimension,
                    totalEmbeddingVectors);

                if (correlation > bestCorrelation)
                {
                    bestCorrelation = correlation;
                }
            }
            return bestCorrelation;
        }


        private double ComputeCorrelationForLibrary(
            List<double> normalizedSource,
            EmbeddingResult targetEmbedding,
            EmbeddingResult sourceEmbedding,
            int librarySize,
            int embeddingDimension,
            int totalVectors)
        {
            int predictionCount = totalVectors - librarySize;

            double[][] libraryVectors =
                targetEmbedding.Vectors.Take(librarySize).ToArray();

            double[] librarySourceValues = sourceEmbedding.AnchorIndices
                .Take(librarySize)
                .Select(anchorIndex => normalizedSource[anchorIndex])
                .ToArray();

            int neighborCount = embeddingDimension + 1;

            List<double> predictedSourceValues = new List<double>(predictionCount);
            List<double> actualSourceValues = new List<double>(predictionCount);

            for (int predictionIndex = librarySize; predictionIndex < totalVectors; predictionIndex++)
            {
                double[] queryVector = targetEmbedding.Vectors[predictionIndex];

                List<Neighbor> nearestNeighbors =
                    FindNearestNeighbors(libraryVectors, queryVector, neighborCount);

                bool hasEnoughNeighbors = nearestNeighbors.Count >= neighborCount;
                if (!hasEnoughNeighbors)
                {
                    continue;
                }

                double predictedValue =
                    ComputePredictedValue(librarySourceValues, nearestNeighbors);

                double actualValue =
                    normalizedSource[sourceEmbedding.AnchorIndices[predictionIndex]];

                if (!double.IsNaN(predictedValue) && !double.IsNaN(actualValue))
                {
                    predictedSourceValues.Add(predictedValue);
                    actualSourceValues.Add(actualValue);
                }
            }

            return ComputePearsonCorrelation(predictedSourceValues, actualSourceValues);
        }

        private double ComputePredictedValue(
            double[] sourceValues,
            List<Neighbor> neighbors)
        {
            double smallestDistance = Math.Max(neighbors[0].Distance, 1e-12);
            double[] weights = ComputeSoftmaxWeights(neighbors, smallestDistance);

            double predictedValue = 0.0;

            for (int weightIndex = 0; weightIndex < neighbors.Count; weightIndex++)
            {
                int sourceIndex = neighbors[weightIndex].Index;
                predictedValue += weights[weightIndex] * sourceValues[sourceIndex];
            }
            return predictedValue;
        }

        private EmbeddingResult BuildTakensEmbedding(
            List<double> series,
            int embeddingDimension,
            int timeDelay)
        {
            int maxStartIndex =
                series.Count - (embeddingDimension - 1) * timeDelay;

            List<double[]> embeddingVectors = new List<double[]>(maxStartIndex);
            List<int> anchorIndices = new List<int>(maxStartIndex);

            for (int startIndex = 0; startIndex < maxStartIndex; startIndex++)
            {
                double[] reconstructedVector = new double[embeddingDimension];

                for (int dimensionIndex = 0; dimensionIndex < embeddingDimension; dimensionIndex++)
                {
                    reconstructedVector[dimensionIndex] =
                        series[startIndex + (dimensionIndex * timeDelay)];
                }

                embeddingVectors.Add(reconstructedVector);
                anchorIndices.Add(startIndex + (embeddingDimension - 1) * timeDelay);
            }
            return new EmbeddingResult(embeddingVectors, anchorIndices);
        }

        private List<Neighbor> FindNearestNeighbors(
            double[][] libraryVectors,
            double[] queryVector,
            int neighborCount)
        {
            List<Neighbor> neighbors = new List<Neighbor>(libraryVectors.Length);

            for (int libraryIndex = 0; libraryIndex < libraryVectors.Length; libraryIndex++)
            {
                double distance = ComputeEuclideanDistance(libraryVectors[libraryIndex], queryVector);
                neighbors.Add(new Neighbor(libraryIndex, distance));
            }

            neighbors.Sort((left, right) => left.Distance.CompareTo(right.Distance));
            return neighbors.Take(neighborCount).ToList();
        }

        private double[] ComputeSoftmaxWeights(
            List<Neighbor> neighbors,
            double smallestDistance)
        {
            int neighborCount = neighbors.Count;
            double[] weights = new double[neighborCount];
            double totalWeight = 0.0;

            for (int neighborIndex = 0; neighborIndex < neighborCount; neighborIndex++)
            {
                double distance = neighbors[neighborIndex].Distance;
                double weight = Math.Exp(-distance / smallestDistance);

                weights[neighborIndex] = weight;
                totalWeight += weight;
            }

            for (int weightIndex = 0; weightIndex < neighborCount; weightIndex++)
            {
                weights[weightIndex] /= totalWeight;
            }
            return weights;
        }

        private double ComputeEuclideanDistance(double[] vectorA, double[] vectorB)
        {
            double squaredSum = 0.0;

            for (int dimensionIndex = 0; dimensionIndex < vectorA.Length; dimensionIndex++)
            {
                double delta = vectorA[dimensionIndex] - vectorB[dimensionIndex];
                squaredSum += delta * delta;
            }

            return Math.Sqrt(squaredSum);
        }

        private List<double> NormalizeZScore(List<double> series)
        {
            double mean = series.Average();
            double variance = series.Sum(value => Math.Pow(value - mean, 2)) / series.Count;
            double stdDev = Math.Sqrt(variance);

            if (stdDev == 0.0)
            {
                return series.Select(_ => 0.0).ToList();
            }
            return series.Select(value => (value - mean) / stdDev).ToList();
        }

        private double ComputePearsonCorrelation(
            List<double> firstValues,
            List<double> secondValues)
        {
            int sampleCount = Math.Min(firstValues.Count, secondValues.Count);
            
            double meanFirst = firstValues.Average();
            double meanSecond = secondValues.Average();

            double covariance = 0.0;
            double varianceFirst = 0.0;
            double varianceSecond = 0.0;

            for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                double firstDeviation = firstValues[sampleIndex] - meanFirst;
                double secondDeviation = secondValues[sampleIndex] - meanSecond;

                covariance += firstDeviation * secondDeviation;
                varianceFirst += firstDeviation * firstDeviation;
                varianceSecond += secondDeviation * secondDeviation;
            }

            double denominator = Math.Sqrt(varianceFirst * varianceSecond);

            return covariance / denominator;
        }
    }
}
