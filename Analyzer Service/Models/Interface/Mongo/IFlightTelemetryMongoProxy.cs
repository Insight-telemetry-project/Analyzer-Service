using Analyzer_Service.Models.Configuration;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Ro.Algorithms;
using Analyzer_Service.Models.Schema;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Analyzer_Service.Models.Interface.Mongo
{
    public interface IFlightTelemetryMongoProxy
    {
        Task<IAsyncCursor<TelemetrySensorFields>> GetCursorFromFieldsAsync(int masterIndex);
        Task<List<TelemetryFlightData>> GetFromFlightDataAsync(int masterIndex);
        Task<int> GetFlightLengthAsync(int masterIndex);
        Task StoreConnectionsBulkAsync(List<ConnectionResult> connections);
        Task StoreAnomalyAsync(int masterIndex, string sensorName, double anomalyTime);
         Task<IAsyncCursor<HistoricalAnomalyRecord>> GetHistoricalCandidatesAsync(string parameterName, string label, int excludeMasterIndex);
         Task StoreHistoricalAnomalyAsync(HistoricalAnomalyRecord record);

        Task<CachedFlightData> GetOrLoadFlightCacheAsync(int masterIndex);

         Task<List<HistoricalAnomalyRecord>> GetAllPointsByFlightNumber(int masterIndex);

        Task<List<HistoricalAnomalyRecord>> GetHistoricalCandidatesByParameterAsync(string parameterName, int excludeMasterIndex);
        Task StoreHistoricalSimilarityAsync(int masterIndex,string parameterName,List<HistoricalSimilarityPoint> points);

    }
}
