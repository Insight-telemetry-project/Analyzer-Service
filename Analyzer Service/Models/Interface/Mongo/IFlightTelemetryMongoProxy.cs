using Analyzer_Service.Models.Configuration;
using Analyzer_Service.Models.Ro.Algorithms;
using Analyzer_Service.Models.Schema;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Analyzer_Service.Models.Interface.Mongo
{
    public interface IFlightTelemetryMongoProxy
    {
        public Task<IAsyncCursor<TelemetrySensorFields>> GetCursorFromFieldsAsync(int masterIndex);
        public Task<List<TelemetryFlightData>> GetFromFlightDataAsync(int masterIndex);
        public Task<int> GetFlightLengthAsync(int masterIndex);
        public Task StoreConnectionsBulkAsync(List<ConnectionResult> connections);
        public Task StoreAnomalyAsync(int masterIndex, string sensorName, double anomalyTime);
        public Task<IAsyncCursor<HistoricalAnomalyRecord>> GetHistoricalCandidatesAsync(string parameterName, string label, int excludeMasterIndex);
        public Task StoreHistoricalAnomalyAsync(HistoricalAnomalyRecord record);

    }
}
