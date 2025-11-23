namespace Analyzer_Service.Models.Interface.Algorithms.HistoricalAnomaly
{
    public interface IHistoricalAnomalySimilarityLogic
    {
        double CompareHashesFuzzy(double[] hashA, double[] hashB);
        double CompareFeatureVectors(Dictionary<string, double> featuresA, Dictionary<string, double> featuresB);
        double CompareDurationSimilarity(double durationA, double durationB);
        double ComputeWeightedScore(double hashSimilarity, double featureSimilarity, double durationSimilarity);
    }
}
