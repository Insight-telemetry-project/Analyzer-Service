using Analyzer_Service.Models.Configuration;
using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Models.Schema;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Analyzer_Service.Services.Mongo
{
    public class FlightTelemetryMongoProxy: IFlightTelemetryMongoProxy
    {
        private readonly IMongoCollection<TelemetrySensorFields> _telemetryFields;
        private readonly IMongoCollection<TelemetryFlightData> _telemetryFlightData;

        public FlightTelemetryMongoProxy(IOptions<MongoSettings> settings)
        {
            MongoSettings mongoSettings = settings.Value;

            MongoClient client = new MongoClient(mongoSettings.ConnectionString);
            IMongoDatabase database = client.GetDatabase(mongoSettings.DatabaseName);

            _telemetryFields = database.GetCollection<TelemetrySensorFields>(mongoSettings.CollectionTelemetryFields);
            _telemetryFlightData = database.GetCollection<TelemetryFlightData>(mongoSettings.CollectionTelemetryFlightData);
        }
        public async Task<List<TelemetrySensorFields>> GetFromFieldsAsync(int masterIndex)
        {
            FilterDefinition<TelemetrySensorFields> filter =
                Builders<TelemetrySensorFields>.Filter.Eq(ConstantFligth.FLIGHT_ID, masterIndex);

            List<TelemetrySensorFields> results = await _telemetryFields.Find(filter).ToListAsync();
            return results;
        }

        public async Task<List<TelemetryFlightData>> GetFromFlightDataAsync(int masterIndex)
        {
            FilterDefinition<TelemetryFlightData> filter =
                Builders<TelemetryFlightData>.Filter.Eq(ConstantFligth.FLIGHT_ID, masterIndex);

            List<TelemetryFlightData> results = await _telemetryFlightData.Find(filter).ToListAsync();
            return results;
        }
    }
}
