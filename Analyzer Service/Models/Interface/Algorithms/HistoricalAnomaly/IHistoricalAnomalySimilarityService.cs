using Analyzer_Service.Models.Ro.Algorithms;

namespace Analyzer_Service.Models.Interface.Algorithms.HistoricalAnomaly
{
    public interface IHistoricalAnomalySimilarityService
    {
        Task<List<HistoricalSimilarityResult>> FindSimilarAnomaliesAsync(int masterIndex,string parameterName);
    }
}
