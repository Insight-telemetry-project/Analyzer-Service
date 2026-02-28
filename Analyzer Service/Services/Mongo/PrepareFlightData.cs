using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Models.Schema;
using MongoDB.Driver;

public class PrepareFlightData : IPrepareFlightData
{
    private readonly IFlightTelemetryMongoProxy _telemetryMongo;

    public PrepareFlightData(IFlightTelemetryMongoProxy telemetryMongo)
    {
<<<<<<< Updated upstream
        _telemetryMongo = telemetryMongo;
    }

    public async Task<double[]> GetParameterValuesAsync(int masterIndex, string parameterName)
    {
        return await _telemetryMongo.GetParameterSeriesAsync(masterIndex, parameterName);
    }

    public async Task<double[]> PrepareYAsync(int masterIndex, string fieldName)
    {
        IAsyncCursor<TelemetrySensorFields> cursor =
            await _telemetryMongo.GetCursorFromFieldsAsync(masterIndex);

        List<double> values = new List<double>();

        await foreach (TelemetrySensorFields record in cursor.ToAsyncEnumerable())
        {
            if (record.Fields.TryGetValue(fieldName, out double val))
=======
        private readonly IFlightTelemetryMongoProxy _telemetryMongoProxy; // Discussion: rename: generic name _telemetryMongo

        public PrepareFlightData(IFlightTelemetryMongoProxy telemetryMongoProxy)
        {
            _telemetryMongoProxy = telemetryMongoProxy;
        }

        public async Task<SignalSeries> PrepareFlightDataAsync(int masterIndex, string xParameter, string yParameter)
        {
            IAsyncCursor<TelemetrySensorFields> cursor = await _telemetryMongoProxy.GetCursorFromFieldsAsync(masterIndex);

            List<double> xSeries = new List<double>();
            List<double> ySeries = new List<double>();

            await foreach (TelemetrySensorFields record in cursor.ToAsyncEnumerable()) // Discussion: is it correct, using await foreach here?
                                                                                       // Dicussion: going through all the frames in the flight here manually, can do it with mongo queries, aggregation pipeline
>>>>>>> Stashed changes
            {
                values.Add(val);
            }
        }

        return values.ToArray();
    }

    public async Task<List<HistoricalAnomalyRecord>> GetFlightPointsByParameterAsync(
        int masterIndex,
        string parameterName)
    {
        List<HistoricalAnomalyRecord> allPoints =
            await _telemetryMongo.GetAllPointsByFlightNumber(masterIndex);

        List<HistoricalAnomalyRecord> filtered =
            new List<HistoricalAnomalyRecord>();

        for (int index = 0; index < allPoints.Count; index++)
        {
<<<<<<< Updated upstream
            HistoricalAnomalyRecord record = allPoints[index];

            if (record.ParameterName == parameterName)
=======
            IAsyncCursor<TelemetrySensorFields> cursor =
                await _telemetryMongoProxy.GetCursorFromFieldsAsync(masterIndex);

            List<double> ySeries = new List<double>(); // Discussion: meaningful names?

            await foreach (TelemetrySensorFields record in cursor.ToAsyncEnumerable()) // Discussion: same as PrepareFlightDataAsync
>>>>>>> Stashed changes
            {
                filtered.Add(record);
            }
        }

        return filtered;
    }

    public async Task<long> GetFlightStartEpochSecondsAsync(int masterIndex)
    {
        return await _telemetryMongo.GetFlightStartEpochSecondsAsync(masterIndex);
    }
}
