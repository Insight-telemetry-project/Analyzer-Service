using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Models.Schema;
using MongoDB.Driver;

public class PrepareFlightData : IPrepareFlightData
{
    private readonly IFlightTelemetryMongoProxy _telemetryMongo;

    public PrepareFlightData(IFlightTelemetryMongoProxy telemetryMongo)
    {
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
            HistoricalAnomalyRecord record = allPoints[index];

            if (record.ParameterName == parameterName)
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
