using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Models.Schema;
using Microsoft.Extensions.Caching.Memory;
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

        //public async Task<SignalSeries> PrepareFlightDataAsync(int masterIndex, string xParameter, string yParameter)
        //{
        //    IAsyncCursor<TelemetrySensorFields> cursor = await _telemetryMongo.GetCursorFromFieldsAsync(masterIndex);

        //    List<double> xSeries = new List<double>();
        //    List<double> ySeries = new List<double>();

        //    await foreach (TelemetrySensorFields record in cursor.ToAsyncEnumerable())
        //    {
        //        if (record.Fields.TryGetValue(yParameter, out double yValue))
        //        {
        //            xSeries.Add(record.Timestep);
        //            ySeries.Add(yValue);
        //        }
        //    }

        //    return new SignalSeries(xSeries, ySeries);
        //}

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




        public async Task<IReadOnlyList<double>> GetParameterValuesAsync(int masterIndex, string parameterName)
        {
            return await _telemetryMongo.GetParameterSeriesAsync(masterIndex, parameterName);
        }






        public async Task<List<double>> GetParameterValuesCopyAsync(int masterIndex, string parameterName)
        {
            return await _telemetryMongo.GetParameterSeriesAsync(masterIndex, parameterName);           
        }








        private async Task<List<HistoricalAnomalyRecord>> GetAllHistoricalPointsByFlightAsync(int masterIndex)
        {
            List<HistoricalAnomalyRecord> results =
                await _telemetryMongo.GetAllPointsByFlightNumber(masterIndex);
            return results;
        }

        public async Task<List<HistoricalAnomalyRecord>> GetFlightPointsByParameterAsync(int masterIndex, string parameterName)
        {
            List<HistoricalAnomalyRecord> allPoints = await GetAllHistoricalPointsByFlightAsync(masterIndex);

            List<HistoricalAnomalyRecord> filtered = new List<HistoricalAnomalyRecord>();

            for (int indexPoints = 0; indexPoints < allPoints.Count; indexPoints++)
            {
                HistoricalAnomalyRecord record = allPoints[indexPoints];

                if (record.ParameterName == parameterName)
                {
                    filtered.Add(record);
                }
            }

            return filtered;
        }

        public async Task<long> GetFlightStartEpochSecondsAsync(int masterIndex)
        {
            long startEpoch = await _telemetryMongo.GetFlightStartEpochSecondsAsync(masterIndex);
            return startEpoch;
        }

    }
}
