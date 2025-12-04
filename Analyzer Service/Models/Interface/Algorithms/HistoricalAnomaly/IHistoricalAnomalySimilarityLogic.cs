namespace Analyzer_Service.Models.Interface.Algorithms.HistoricalAnomaly
{
    public interface IHistoricalAnomalySimilarityLogic
    {
        public double CompareHashesFuzzy(double[] hashA, double[] hashB);
        public double CompareFeatureVectors(Dictionary<string, double> featuresA, Dictionary<string, double> featuresB);
        public double CompareDurationSimilarity(double durationA, double durationB);
        public double ComputeWeightedScore(double hashSimilarity, double featureSimilarity, double durationSimilarity);
    }
}
