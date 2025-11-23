using Analyzer_Service.Models.Ro.Algorithms;

namespace Analyzer_Service.Models.Interface.Algorithms.HistoricalAnomaly
{
    public interface IHistoricalAnomalySimilarityService
    {
        Task<List<HistoricalSimilarityResult>> FindSimilarAnomaliesAsync(
            string parameterName,
            string label,
            double[] newHashVector,
            Dictionary<string, double> newFeatureVector,
            double newDuration,
            double threshold);
    }
}
