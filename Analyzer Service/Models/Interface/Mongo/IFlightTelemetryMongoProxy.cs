using Analyzer_Service.Models.Configuration;
using Analyzer_Service.Models.Ro.Algorithms;
using Analyzer_Service.Models.Schema;
using Microsoft.Extensions.Options;

namespace Analyzer_Service.Models.Interface.Mongo
{
    public interface IFlightTelemetryMongoProxy
    {
        Task<List<TelemetrySensorFields>> GetFromFieldsAsync(int masterIndex);
        Task<List<TelemetryFlightData>> GetFromFlightDataAsync(int masterIndex);
        Task<int> GetFlightLengthAsync(int masterIndex);
        Task StoreConnectionsBulkAsync(List<ConnectionResult> connections);
        Task StoreAnomalyAsync(int masterIndex, string sensorName, double anomalyTime);

    }
}
