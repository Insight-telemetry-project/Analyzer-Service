using Analyzer_Service.Models.Configuration;
using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Models.Ro.Algorithms;
using Analyzer_Service.Models.Schema;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Collections.Concurrent;

namespace Analyzer_Service.Services.Mongo
{
    public class FlightTelemetryMongoProxy: IFlightTelemetryMongoProxy
    {
        private readonly IMongoCollection<TelemetrySensorFields> _telemetryFields;
        private readonly IMongoCollection<TelemetryFlightData> _telemetryFlightData;
        private readonly IMongoCollection<HistoricalAnomalyRecord> _historicalAnomalies;

        private static readonly ConcurrentDictionary<int, Lazy<Task<CachedFlightData>>> FlightCache =
            new ConcurrentDictionary<int, Lazy<Task<CachedFlightData>>>();


        public FlightTelemetryMongoProxy(IOptions<MongoSettings> settings)
        {
            MongoSettings mongoSettings = settings.Value;

            MongoClient client = new MongoClient(mongoSettings.ConnectionString);
            IMongoDatabase database = client.GetDatabase(mongoSettings.DatabaseName);

            _telemetryFields = database.GetCollection<TelemetrySensorFields>(mongoSettings.CollectionTelemetryFields);
            _telemetryFlightData = database.GetCollection<TelemetryFlightData>(mongoSettings.CollectionTelemetryFlightData);
            _historicalAnomalies =database.GetCollection<HistoricalAnomalyRecord>(mongoSettings.CollectionHistoricalAnomalies);

        }


        public async Task<IAsyncCursor<TelemetrySensorFields>> GetCursorFromFieldsAsync(int masterIndex)
        {
            FilterDefinition<TelemetrySensorFields> filter =Builders<TelemetrySensorFields>.Filter.Eq(flight => flight.MasterIndex, masterIndex);

            return await _telemetryFields
                .Find(filter)
                .SortBy(flight => flight.Timestep)
                .ToCursorAsync();
        }



        public async Task<List<TelemetryFlightData>> GetFromFlightDataAsync(int masterIndex)
        {
            FilterDefinition<TelemetryFlightData> filter = Builders<TelemetryFlightData>.Filter.Eq(ConstantFligth.FLIGHT_ID, masterIndex);

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

        

        public async Task StoreConnectionsBulkAsync(List<ConnectionResult> connections)
        {

            int masterIndex = connections[0].MasterIndex;

            FilterDefinition<TelemetryFlightData> filter =
                Builders<TelemetryFlightData>.Filter.Eq(flight => flight.MasterIndex, masterIndex);

            List<WriteModel<TelemetryFlightData>> bulkOperations =new List<WriteModel<TelemetryFlightData>>();

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


        public async Task<IAsyncCursor<HistoricalAnomalyRecord>>GetHistoricalCandidatesAsync
            (string parameterName, string label, int masterIndex)
        {
            FilterDefinition<HistoricalAnomalyRecord> filter =
                Builders<HistoricalAnomalyRecord>.Filter.Eq(flight => flight.ParameterName, parameterName) &
                Builders<HistoricalAnomalyRecord>.Filter.Eq(flight => flight.Label, label) &
                Builders<HistoricalAnomalyRecord>.Filter.Ne(flight => flight.MasterIndex, masterIndex);

            return await _historicalAnomalies
                .Find(filter)
                .ToCursorAsync();
        }

        public async Task StoreHistoricalAnomalyAsync(HistoricalAnomalyRecord record)
        {
            FilterDefinition<HistoricalAnomalyRecord> filter =
                Builders<HistoricalAnomalyRecord>.Filter.Eq(flight => flight.MasterIndex, record.MasterIndex) &
                Builders<HistoricalAnomalyRecord>.Filter.Eq(flight => flight.ParameterName, record.ParameterName) &
                Builders<HistoricalAnomalyRecord>.Filter.Eq(flight => flight.StartIndex, record.StartIndex) &
                Builders<HistoricalAnomalyRecord>.Filter.Eq(flight => flight.EndIndex, record.EndIndex);

            bool exists = await _historicalAnomalies.Find(filter).AnyAsync();

            if (!exists)
            {
                await _historicalAnomalies.InsertOneAsync(record);
            }
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
















        public Task<CachedFlightData> GetOrLoadFlightCacheAsync(int masterIndex)
        {
            Lazy<Task<CachedFlightData>> loader = FlightCache.GetOrAdd(
                masterIndex,
                _ => new Lazy<Task<CachedFlightData>>(() => LoadFlightCacheAsync(masterIndex)));

            return loader.Value;
        }

        private async Task<CachedFlightData> LoadFlightCacheAsync(int masterIndex)
        {
            IAsyncCursor<TelemetrySensorFields> cursor =
                await GetCursorFromFieldsAsync(masterIndex);

            Dictionary<string, List<double>> fieldValues =
                new Dictionary<string, List<double>>();

            await foreach (TelemetrySensorFields record in cursor.ToAsyncEnumerable())
            {
                foreach (KeyValuePair<string, double> pair in record.Fields)
                {
                    if (!fieldValues.TryGetValue(pair.Key, out List<double> values))
                    {
                        values = new List<double>();
                        fieldValues[pair.Key] = values;
                    }

                    values.Add(pair.Value);
                }
            }

            return new CachedFlightData(fieldValues);
        }

        public async Task<List<HistoricalAnomalyRecord>> GetAllPointsByFlightNumber(int masterIndex)
        {
                        FilterDefinition<HistoricalAnomalyRecord> filter =
                Builders<HistoricalAnomalyRecord>.Filter.Eq(flight => flight.MasterIndex, masterIndex);
            List<HistoricalAnomalyRecord> results = await _historicalAnomalies.Find(filter).ToListAsync();
            return results;
        }


        public async Task<List<HistoricalAnomalyRecord>> GetHistoricalCandidatesByParameterAsync(string parameterName, int excludeMasterIndex)
        {
            FilterDefinition<HistoricalAnomalyRecord> filter =
                Builders<HistoricalAnomalyRecord>.Filter.Eq(record => record.ParameterName, parameterName) &
                Builders<HistoricalAnomalyRecord>.Filter.Ne(record => record.MasterIndex, excludeMasterIndex);

            List<HistoricalAnomalyRecord> results =
                await _historicalAnomalies.Find(filter).ToListAsync();

            return results;
        }
        public async Task StoreHistoricalSimilarityAsync(int masterIndex,string parameterName,List<HistoricalSimilarityPoint> points)
        {
            FilterDefinition<TelemetryFlightData> filter =
                Builders<TelemetryFlightData>.Filter.Eq(flight => flight.MasterIndex, masterIndex);

            UpdateDefinition<TelemetryFlightData> update =
                Builders<TelemetryFlightData>.Update
                    .Set($"HistoricalSimilarity.{parameterName}", points)
                    .SetOnInsert("Master Index", masterIndex);

            await _telemetryFlightData.UpdateOneAsync(
                filter,
                update,
                new UpdateOptions { IsUpsert = true });
        }

    }
}
