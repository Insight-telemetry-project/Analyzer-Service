using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Models.Schema;
using MongoDB.Driver;

namespace Analyzer_Service.Services.Mongo
{
    public class PrepareFlightData: IPrepareFlightData
    {
        private readonly IFlightTelemetryMongoProxy _telemetryMongo;

        public PrepareFlightData(IFlightTelemetryMongoProxy telemetryMongo)
        {
            _telemetryMongo = telemetryMongo;
        }

        public async Task<SignalSeries> PrepareFlightDataAsync(int masterIndex, string xParameter, string yParameter)
        {
            IAsyncCursor<TelemetrySensorFields> cursor = await _telemetryMongo.GetCursorFromFieldsAsync(masterIndex);

            List<double> xSeries = new List<double>();
            List<double> ySeries = new List<double>();

            await foreach (TelemetrySensorFields record in cursor.ToAsyncEnumerable())
            {
                if (record.Fields.TryGetValue(yParameter, out double yValue))
                {
                    xSeries.Add(record.Timestep);
                    ySeries.Add(yValue);
                }
            }

            return new SignalSeries(xSeries, ySeries);
        }

        public async Task<List<double>> PrepareYAsync(int masterIndex, string fieldName)
        {
            IAsyncCursor<TelemetrySensorFields> cursor =
                await _telemetryMongo.GetCursorFromFieldsAsync(masterIndex);

            List<double> ySeries = new List<double>();

            await foreach (TelemetrySensorFields record in cursor.ToAsyncEnumerable())
            {
                if (record.Fields.TryGetValue(fieldName, out double val))
                {
                    ySeries.Add(val);
                }
            }

            return ySeries;
        }


        public async Task<List<double>> GetParameterValuesAsync(int masterIndex, string parameterName)
        {
            CachedFlightData cached = await _telemetryMongo.GetOrLoadFlightCacheAsync(masterIndex);

            if (!cached.FieldValues.TryGetValue(parameterName, out List<double> values))
            {
                return new List<double>();
            }

            return values;
        }






        public async Task<List<double>> GetParameterValuesCopyAsync(int masterIndex, string parameterName)
        {
            CachedFlightData cached = await _telemetryMongo.GetOrLoadFlightCacheAsync(masterIndex);

            if (!cached.FieldValues.TryGetValue(parameterName, out List<double> values))
            {
                return new List<double>();
            }

            return new List<double>(values);
        }


    }
}
