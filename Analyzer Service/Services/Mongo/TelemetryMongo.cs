using Analyzer_Service.Models.Configuration;
using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Schema;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Analyzer_Service.Services.Mongo
{
    public class TelemetryMongo
    {
        private readonly IMongoCollection<TelemetryFieldsRecord> _telemetryFields;
        private readonly IMongoCollection<TelemetryFlightDataRecord> _telemetryFlightData;

        public TelemetryMongo(IOptions<MongoSettings> settings)
        {
            MongoSettings mongoSettings = settings.Value;

            MongoClient client = new MongoClient(mongoSettings.ConnectionString);
            IMongoDatabase database = client.GetDatabase(mongoSettings.DatabaseName);

            _telemetryFields = database.GetCollection<TelemetryFieldsRecord>(mongoSettings.CollectionTelemetryFields);
            _telemetryFlightData = database.GetCollection<TelemetryFlightDataRecord>(mongoSettings.CollectionTelemetryFlightData);
        }
        public async Task<List<TelemetryFieldsRecord>> GetFromFieldsAsync(int masterIndex)
        {
            FilterDefinition<TelemetryFieldsRecord> filter =
                Builders<TelemetryFieldsRecord>.Filter.Eq(ConstantFligth.FLIGHT_ID, masterIndex);

            List<TelemetryFieldsRecord> results = await _telemetryFields.Find(filter).ToListAsync();
            return results;
        }

        public async Task<List<TelemetryFlightDataRecord>> GetFromFlightDataAsync(int masterIndex)
        {
            FilterDefinition<TelemetryFlightDataRecord> filter =
                Builders<TelemetryFlightDataRecord>.Filter.Eq(ConstantFligth.FLIGHT_ID, masterIndex);

            List<TelemetryFlightDataRecord> results = await _telemetryFlightData.Find(filter).ToListAsync();
            return results;
        }
    }
}
