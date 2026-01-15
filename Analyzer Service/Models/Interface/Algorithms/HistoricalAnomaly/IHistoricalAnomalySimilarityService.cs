using Analyzer_Service.Models.Enums;
using Analyzer_Service.Models.Ro.Algorithms;

namespace Analyzer_Service.Models.Interface.Algorithms.HistoricalAnomaly
{
    public interface IHistoricalAnomalySimilarityService
    {
        public Task<List<HistoricalSimilarityResult>> FindSimilarAnomaliesAsync(int masterIndex,string parameterName,flightStatus status);
    }
}
