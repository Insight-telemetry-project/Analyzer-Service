using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Models.Schema;

namespace Analyzer_Service.Services.Mongo
{
    public class PrepareFlightData: IPrepareFlightData
    {
        private readonly IFlightTelemetryMongoProxy _telemetryMongo;

        public PrepareFlightData(IFlightTelemetryMongoProxy telemetryMongo)
        {
            _telemetryMongo = telemetryMongo;
        }

        public async Task<(List<double> X, List<double> Y)> PrepareFlightDataAsync(
    int masterIndex, string xField, string yField)
        {
            List<TelemetrySensorFields> records = await _telemetryMongo.GetFromFieldsAsync(masterIndex);
            List<TelemetrySensorFields> ordered = records.OrderBy(record => record.Timestep).ToList();

            List<double> xSeries = new();
            List<double> ySeries = new();

            foreach (TelemetrySensorFields record in ordered)
            {
                if (record.Fields.TryGetValue(yField, out double yValue))
                {
                    xSeries.Add(record.Timestep);
                    ySeries.Add(yValue);
                }
            }

            return (xSeries, ySeries);
        }

    }
}
