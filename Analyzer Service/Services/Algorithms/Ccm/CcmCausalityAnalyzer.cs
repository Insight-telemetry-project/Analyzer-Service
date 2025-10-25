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
            if (sourceSeries == null || targetSeries == null || sourceSeries.Count != targetSeries.Count)
            {
                throw new ArgumentException("Input series must be non-null and have the same length.");
            }

            if (embeddingDimension < 2 || timeDelay < 1)
            {
                throw new ArgumentException("Invalid embedding parameters.");
            }

            int totalLength = sourceSeries.Count;
            int minimumRequired = (embeddingDimension - 1) * timeDelay + 1;

            if (totalLength < minimumRequired + 10)
            {
                return 0.0;
            }

            List<double> normalizedSource = NormalizeZScore(sourceSeries);
            List<double> normalizedTarget = NormalizeZScore(targetSeries);

            EmbeddingResult sourceEmbedding = BuildTakensEmbedding(normalizedSource, embeddingDimension, timeDelay);
            EmbeddingResult targetEmbedding = BuildTakensEmbedding(normalizedTarget, embeddingDimension, timeDelay);

            int vectorCount = Math.Min(sourceEmbedding.Vectors.Count, targetEmbedding.Vectors.Count);
            if (vectorCount < embeddingDimension + 2)
            {
                return 0.0;
            }

            int minLibrarySize = Math.Max(20, embeddingDimension + 2);
            int maxLibrarySize = Math.Min(500, vectorCount - 5);
            int libraryStep = Math.Max(10, (maxLibrarySize - minLibrarySize) / 10);

            double bestCorrelation = 0.0;

            for (int librarySize = minLibrarySize; librarySize <= maxLibrarySize; librarySize += libraryStep)
            {
                int predictionCount = vectorCount - librarySize;
                if (predictionCount < 5)
                {
                    break;
                }

                double[][] targetLibraryVectors = targetEmbedding.Vectors.Take(librarySize).ToArray();
                double[] sourceValuesInLibrary = sourceEmbedding.AnchorIndices
                    .Take(librarySize)
                    .Select(index => normalizedSource[index])
                    .ToArray();

                int neighborCount = embeddingDimension + 1;
                List<double> estimatedSourceValues = new List<double>(predictionCount);
                List<double> actualSourceValues = new List<double>(predictionCount);

                for (int predictionIndex = librarySize; predictionIndex < vectorCount; predictionIndex++)
                {
                    double[] queryVector = targetEmbedding.Vectors[predictionIndex];
                    List<Neighbor> nearestNeighbors = FindNearestNeighbors(targetLibraryVectors, queryVector, neighborCount);

                    if (nearestNeighbors.Count < neighborCount)
                    {
                        continue;
                    }

                    double smallestDistance = Math.Max(nearestNeighbors[0].Distance, 1e-12);
                    double[] weights = ComputeSoftmaxWeights(nearestNeighbors, smallestDistance);

                    double estimatedValue = 0.0;
                    for (int j = 0; j < neighborCount; j++)
                    {
                        int neighborIndex = nearestNeighbors[j].Index;
                        estimatedValue += weights[j] * sourceValuesInLibrary[neighborIndex];
                    }

                    double actualValue = normalizedSource[sourceEmbedding.AnchorIndices[predictionIndex]];
                    if (!double.IsNaN(estimatedValue) && !double.IsNaN(actualValue))
                    {
                        estimatedSourceValues.Add(estimatedValue);
                        actualSourceValues.Add(actualValue);
                    }
                }

                if (estimatedSourceValues.Count >= 5)
                {
                    double correlation = ComputePearsonCorrelation(estimatedSourceValues, actualSourceValues);
                    if (!double.IsNaN(correlation))
                    {
                        if (correlation > bestCorrelation)
                        {
                            bestCorrelation = correlation;
                        }
                    }
                }
            }

            return bestCorrelation;
        }


        private EmbeddingResult BuildTakensEmbedding(List<double> series, int embeddingDimension, int timeDelay)
        {
            int lastStartIndex = series.Count - (embeddingDimension - 1) * timeDelay;
            List<double[]> embeddingVectors = new List<double[]>(lastStartIndex);
            List<int> anchorIndices = new List<int>(lastStartIndex);

            for (int startIndex = 0; startIndex < lastStartIndex; startIndex++)
            {
                double[] vector = new double[embeddingDimension];
                for (int dim = 0; dim < embeddingDimension; dim++)
                {
                    vector[dim] = series[startIndex + dim * timeDelay];
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
            double sum = 0.0;

            for (int i = 0; i < count; i++)
            {
                weights[i] = Math.Exp(-neighbors[i].Distance / minDistance);
                sum += weights[i];
            }

            for (int i = 0; i < count; i++)
            {
                weights[i] /= sum;
            }

            return weights;
        }

        private double ComputeEuclideanDistance(double[] a, double[] b)
        {
            double sum = 0.0;
            for (int i = 0; i < a.Length; i++)
            {
                double diff = a[i] - b[i];
                sum += diff * diff;
            }
            return Math.Sqrt(sum);
        }

        private List<double> NormalizeZScore(List<double> series)
        {
            double mean = series.Average();
            double variance = series.Sum(x => Math.Pow(x - mean, 2)) / series.Count;
            double std = Math.Sqrt(variance);

            if (std == 0.0)
            {
                return series.Select(_ => 0.0).ToList();
            }

            List<double> normalized = new List<double>(series.Count);
            foreach (double value in series)
            {
                normalized.Add((value - mean) / std);
            }
            return normalized;
        }

        private double ComputePearsonCorrelation(List<double> a, List<double> b)
        {
            int n = Math.Min(a.Count, b.Count);
            if (n == 0)
            {
                return 0.0;
            }

            double meanA = a.Average();
            double meanB = b.Average();

            double numerator = 0.0;
            double sumA = 0.0;
            double sumB = 0.0;

            for (int i = 0; i < n; i++)
            {
                double da = a[i] - meanA;
                double db = b[i] - meanB;
                numerator += da * db;
                sumA += da * da;
                sumB += db * db;
            }

            double denominator = Math.Sqrt(sumA * sumB);
            if (denominator == 0.0)
            {
                return 0.0;
            }

            return numerator / denominator;
        }
    }
}
