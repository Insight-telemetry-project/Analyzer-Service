using System;
using System.Collections.Generic;
using System.Linq;
using Analyzer_Service.Models.Interface.Algorithms.Ccm;
using Analyzer_Service.Models.Algorithms.Ccm;

namespace Analyzer_Service.Services.Algorithms.Ccm
{
    public class CcmCausalityAnalyzer : ICcmCausalityAnalyzer
    {
        public double ComputeCausality(List<double> sourceSeries, List<double> targetSeries, int embeddingDimension, int timeDelay)
        {
            int totalSamples = sourceSeries.Count;
            int requiredSamples = (embeddingDimension - 1) * timeDelay + 1;
            if (totalSamples < requiredSamples + 10)
            {
                return 0.0;
            }
            List<double> normalizedSource = NormalizeZScore(sourceSeries);
            List<double> normalizedTarget = NormalizeZScore(targetSeries);

            EmbeddingResult sourceEmbedding = BuildTakensEmbedding(normalizedSource, embeddingDimension, timeDelay);
            EmbeddingResult targetEmbedding = BuildTakensEmbedding(normalizedTarget, embeddingDimension, timeDelay);

            int totalVectors = Math.Min(sourceEmbedding.Vectors.Count, targetEmbedding.Vectors.Count);
            if (totalVectors < embeddingDimension + 2)
            {
                return 0.0;
            }

            return ComputeBestCorrelation(
                normalizedSource,
                sourceEmbedding,
                targetEmbedding,
                embeddingDimension,
                totalVectors
            );
        }
        private double ComputeBestCorrelation(
            List<double> normalizedSource,
            EmbeddingResult sourceEmbedding,
            EmbeddingResult targetEmbedding,
            int embeddingDimension,
            int totalVectors)
        {
            int minimumLibrarySize = Math.Max(20, embeddingDimension + 2);
            int maximumLibrarySize = Math.Min(500, totalVectors - 5);
            int libraryStep = Math.Max(10, (maximumLibrarySize - minimumLibrarySize) / 10);

            double bestCorrelation = 0.0;

            for (int currentLibrarySize = minimumLibrarySize; currentLibrarySize <= maximumLibrarySize; currentLibrarySize += libraryStep)
            {
                double correlation = ComputeCorrelationForLibrary(
                    normalizedSource,
                    targetEmbedding,
                    sourceEmbedding,
                    currentLibrarySize,
                    embeddingDimension,
                    totalVectors
                );

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
            double[][] libraryVectors = targetEmbedding.Vectors.Take(librarySize).ToArray();
            double[] librarySourceValues = sourceEmbedding.AnchorIndices
                .Take(librarySize)
                .Select(index => normalizedSource[index])
                .ToArray();

            int neighborCount = embeddingDimension + 1;
            List<double> predictedSourceValues = new List<double>(predictionCount);
            List<double> actualSourceValues = new List<double>(predictionCount);

            for (int predictionIndex = librarySize; predictionIndex < totalVectors; predictionIndex++)
            {
                double[] queryVector = targetEmbedding.Vectors[predictionIndex];
                List<Neighbor> nearestNeighbors = FindNearestNeighbors(libraryVectors, queryVector, neighborCount);

                bool hasEnoughNeighbors = nearestNeighbors.Count >= neighborCount;
                if (hasEnoughNeighbors)
                {
                    double predictedValue = ComputePredictedValue(librarySourceValues, nearestNeighbors);
                    double actualValue = normalizedSource[sourceEmbedding.AnchorIndices[predictionIndex]];

                    if (!double.IsNaN(predictedValue) && !double.IsNaN(actualValue))
                    {
                        predictedSourceValues.Add(predictedValue);
                        actualSourceValues.Add(actualValue);
                    }
                }
            }
            return ComputePearsonCorrelation(predictedSourceValues, actualSourceValues);
        }

        private double ComputePredictedValue(double[] sourceValues, List<Neighbor> neighbors)
        {
            double smallestDistance = Math.Max(neighbors[0].Distance, 1e-12);
            double[] weights = ComputeSoftmaxWeights(neighbors, smallestDistance);

            double predictedValue = 0.0;
            for (int i = 0; i < neighbors.Count; i++)
            {
                int neighborIndex = neighbors[i].Index;
                predictedValue += weights[i] * sourceValues[neighborIndex];
            }
            return predictedValue;
        }

        private EmbeddingResult BuildTakensEmbedding(List<double> series, int embeddingDimension, int timeDelay)
        {
            int maxStartIndex = series.Count - (embeddingDimension - 1) * timeDelay;
            List<double[]> embeddingVectors = new List<double[]>(maxStartIndex);
            List<int> anchorIndices = new List<int>(maxStartIndex);

            for (int startIndex = 0; startIndex < maxStartIndex; startIndex++)
            {
                double[] vector = new double[embeddingDimension];
                for (int dimensionIndex = 0; dimensionIndex < embeddingDimension; dimensionIndex++)
                {
                    vector[dimensionIndex] = series[startIndex + dimensionIndex * timeDelay];
                }

                embeddingVectors.Add(vector);
                anchorIndices.Add(startIndex + (embeddingDimension - 1) * timeDelay);
            }
            return new EmbeddingResult(embeddingVectors, anchorIndices);
        }

        private List<Neighbor> FindNearestNeighbors(double[][] libraryVectors, double[] queryVector, int neighborCount)
        {
            List<Neighbor> neighbors = new List<Neighbor>();

            for (int i = 0; i < libraryVectors.Length; i++)
            {
                double distance = ComputeEuclideanDistance(libraryVectors[i], queryVector);
                neighbors.Add(new Neighbor(i, distance));
            }

            neighbors.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            return neighbors.Take(neighborCount).ToList();
        }

        private double[] ComputeSoftmaxWeights(List<Neighbor> neighbors, double minDistance)
        {
            int count = neighbors.Count;
            double[] weights = new double[count];
            double totalWeight = 0.0;

            for (int i = 0; i < count; i++)
            {
                weights[i] = Math.Exp(-neighbors[i].Distance / minDistance);
                totalWeight += weights[i];
            }

            for (int i = 0; i < count; i++)
            {
                weights[i] /= totalWeight;
            }
            return weights;
        }

        private double ComputeEuclideanDistance(double[] vectorA, double[] vectorB)
        {
            double sum = 0.0;
            for (int i = 0; i < vectorA.Length; i++)
            {
                double difference = vectorA[i] - vectorB[i];
                sum += difference * difference;
            }
            return Math.Sqrt(sum);
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

        private double ComputePearsonCorrelation(List<double> firstSeries, List<double> secondSeries)
        {
            int sampleCount = Math.Min(firstSeries.Count, secondSeries.Count);
            if (sampleCount == 0)
            {
                return 0.0;
            }

            double meanFirst = firstSeries.Average();
            double meanSecond = secondSeries.Average();
            double covariance = 0.0;
            double varianceFirst = 0.0;
            double varianceSecond = 0.0;

            for (int i = 0; i < sampleCount; i++)
            {
                double deviationFirst = firstSeries[i] - meanFirst;
                double deviationSecond = secondSeries[i] - meanSecond;
                covariance += deviationFirst * deviationSecond;
                varianceFirst += deviationFirst * deviationFirst;
                varianceSecond += deviationSecond * deviationSecond;
            }

            double denominator = Math.Sqrt(varianceFirst * varianceSecond);
            if (denominator == 0.0)
            {
                return 0.0;
            }

            return covariance / denominator;
        }
    }
}
