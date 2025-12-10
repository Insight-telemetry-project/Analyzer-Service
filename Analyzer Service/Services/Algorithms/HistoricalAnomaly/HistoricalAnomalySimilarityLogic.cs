using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms.HistoricalAnomaly;

namespace Analyzer_Service.Services.Algorithms.HistoricalAnomaly
{
    public class HistoricalAnomalySimilarityLogic : IHistoricalAnomalySimilarityLogic
    {
        public double CompareHashesFuzzy(double[] hashA, double[] hashB)
        {
            int length = hashA.Length;
            int differenceCount = 0;

            for (int index = 0; index < length; index++)
            {
                if (Math.Abs(hashA[index] - hashB[index]) > ConstantAnomalyDetection.HASH_THRESHOLD)
                {
                    differenceCount++;
                }
            }

            double similarity = 1.0 - ((double)differenceCount / (double)length);
            return similarity;
        }

        public double CompareFeatureVectors(SegmentFeatures featuresA, SegmentFeatures featuresB)
        {
            double[] vectorA = new double[]
            {
                featuresA.DurationSeconds,
                featuresA.MeanZ,
                featuresA.StdZ,
                featuresA.MinZ,
                featuresA.MaxZ,
                featuresA.RangeZ,
                featuresA.EnergyZ,
                featuresA.Slope,
                featuresA.PeakCount,
                featuresA.TroughCount,
                featuresA.MeanPrev,
                featuresA.MeanNext
            };

            double[] vectorB = new double[]
            {
                featuresB.DurationSeconds,
                featuresB.MeanZ,
                featuresB.StdZ,
                featuresB.MinZ,
                featuresB.MaxZ,
                featuresB.RangeZ,
                featuresB.EnergyZ,
                featuresB.Slope,
                featuresB.PeakCount,
                featuresB.TroughCount,
                featuresB.MeanPrev,
                featuresB.MeanNext
            };

            double dotProduct = 0.0;
            double normA = 0.0;
            double normB = 0.0;

            for (int index = 0; index < vectorA.Length; index++)
            {
                dotProduct += vectorA[index] * vectorB[index];
                normA += vectorA[index] * vectorA[index];
                normB += vectorB[index] * vectorB[index];
            }

            double denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
            if (denominator <= 0.0)
            {
                return 0.0;
            }

            double cosineSimilarity = dotProduct / denominator;
            return cosineSimilarity;
        }

        public double CompareDurationSimilarity(double durationA, double durationB)
        {
            double maxDuration = Math.Max(durationA, durationB);
            if (maxDuration <= 0.0)
            {
                return 0.0;
            }

            double similarity = 1.0 - (Math.Abs(durationA - durationB) / maxDuration);
            return similarity;
        }

        public double ComputeWeightedScore(double hashSimilarity, double featureSimilarity, double durationSimilarity)
        {
            double finalScore =
                (ConstantAnomalyDetection.HASH_SIMILARITY * hashSimilarity) +
                (ConstantAnomalyDetection.FEATURE_SIMILARITY * featureSimilarity) +
                (ConstantAnomalyDetection.DURATION_SIMILARITY * durationSimilarity);

            return finalScore;
        }
    }
}
