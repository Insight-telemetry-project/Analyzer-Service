using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Enums;

namespace Analyzer_Service.Models.Interface.Algorithms.HistoricalAnomaly
{
    public interface IHistoricalAnomalySimilarityLogic
    {
        public double CompareHashesFuzzy(double[] hashA, double[] hashB, flightStatus status);
        public double CompareFeatureVectors(SegmentFeatures featuresA, SegmentFeatures featuresB);
        public double CompareDurationSimilarity(double durationA, double durationB);
        public double ComputeWeightedScore(double hashSimilarity, double featureSimilarity, double durationSimilarity, flightStatus status);
    }
}
