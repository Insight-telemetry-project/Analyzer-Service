using Analyzer_Service.Models.Configuration;
using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Models.Ro.Algorithms;
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


        public async Task<IAsyncCursor<TelemetrySensorFields>> GetCursorFromFieldsAsync(int masterIndex)
        {
            FilterDefinition<TelemetrySensorFields> filter =
                Builders<TelemetrySensorFields>.Filter.Eq(flight => flight.MasterIndex, masterIndex);

            return await _telemetryFields
                .Find(filter)
                .SortBy(flight => flight.Timestep)
                .ToCursorAsync();
        }



        public async Task<List<TelemetryFlightData>> GetFromFlightDataAsync(int masterIndex)
        {
            FilterDefinition<TelemetryFlightData> filter =
                Builders<TelemetryFlightData>.Filter.Eq(ConstantFligth.FLIGHT_ID, masterIndex);

            List<TelemetryFlightData> results = await _telemetryFlightData.Find(filter).ToListAsync();
            return results;
        }

       
        public async Task<int> GetFlightLengthAsync(int masterIndex)
        {
            FilterDefinition<TelemetryFlightData> filter =
                Builders<TelemetryFlightData>.Filter.Eq(flight => flight.MasterIndex, masterIndex);

            TelemetryFlightData? result = await _telemetryFlightData.Find(filter).FirstOrDefaultAsync();

            if (result == null || result.Fields == null || !result.Fields.ContainsKey(ConstantFligth.FLIGHT_LENGTH))
                return -1;

            return result.Fields[ConstantFligth.FLIGHT_LENGTH];
        }

        public async Task StoreAnomalyAsync(int masterIndex, string sensorName, double anomalyTime)
        {
            FilterDefinition<TelemetryFlightData> filter =
                Builders<TelemetryFlightData>.Filter.Eq(flight => flight.MasterIndex, masterIndex);

            UpdateDefinition<TelemetryFlightData> update =
                Builders<TelemetryFlightData>.Update
                    .AddToSet($"Anomalies.{sensorName}", anomalyTime);

            await _telemetryFlightData.UpdateOneAsync(
                filter,
                update,
                new UpdateOptions { IsUpsert = true }
            );
        }

        public async Task StoreConnectionsBulkAsync(List<ConnectionResult> connections)
        {

            int masterIndex = connections[0].MasterIndex;

            FilterDefinition<TelemetryFlightData> filter =
                Builders<TelemetryFlightData>.Filter.Eq(flight => flight.MasterIndex, masterIndex);

            List<WriteModel<TelemetryFlightData>> bulkOperations =
                new List<WriteModel<TelemetryFlightData>>();

            foreach (ConnectionResult connection in connections)
            {
                string sensorName = connection.SourceField;
                string targetName = connection.TargetField;

                UpdateDefinition<TelemetryFlightData> update =
                    Builders<TelemetryFlightData>.Update
                        .AddToSet($"Connections.{sensorName}", targetName);

                UpdateOneModel<TelemetryFlightData> updateOperation =
                    new UpdateOneModel<TelemetryFlightData>(filter, update)
                    { IsUpsert = true };

                bulkOperations.Add(updateOperation);
            }

            await _telemetryFlightData.BulkWriteAsync(bulkOperations);
        }


    }
}
