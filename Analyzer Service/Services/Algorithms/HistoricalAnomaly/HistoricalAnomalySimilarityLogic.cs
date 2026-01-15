using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Enums;
using Analyzer_Service.Models.Interface.Algorithms.HistoricalAnomaly;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Services.Algorithms.Pelt;

namespace Analyzer_Service.Services.Algorithms.HistoricalAnomaly
{
    public class HistoricalAnomalySimilarityLogic : IHistoricalAnomalySimilarityLogic
    {
        private readonly ITuningSettingsFactory _tuningSettingsFactory;
        public HistoricalAnomalySimilarityLogic(ITuningSettingsFactory tuningSettingsFactory) 
        {
            _tuningSettingsFactory = tuningSettingsFactory;
        }
        public double CompareHashesFuzzy(double[] hashA, double[] hashB, flightStatus status)
        {
            PeltTuningSettings settings = _tuningSettingsFactory.Get(status);
            int length = hashA.Length;
            int differenceCount = 0;

            for (int index = 0; index < length; index++)
            {
                if (Math.Abs(hashA[index] - hashB[index]) > settings.HASH_THRESHOLD)
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

        public double ComputeWeightedScore(double hashSimilarity, double featureSimilarity, double durationSimilarity, flightStatus status)
        {
            PeltTuningSettings settings = _tuningSettingsFactory.Get(status);
            double finalScore =
                (settings.HASH_SIMILARITY * hashSimilarity) +
                (settings.FEATURE_SIMILARITY * featureSimilarity) +
                (settings.DURATION_SIMILARITY * durationSimilarity);

            return finalScore;
        }
    }
}
