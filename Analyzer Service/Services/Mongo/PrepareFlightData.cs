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

            List<double> xSeries = new List<double>();
            List<double> ySeries = new List<double>();

            foreach (TelemetrySensorFields record in ordered)
            {
                if (record.Fields.ContainsKey(xField) && record.Fields.ContainsKey(yField))
                {
                    xSeries.Add(record.Fields[xField]);
                    ySeries.Add(record.Fields[yField]);
                }
            }

            return (xSeries, ySeries);
        }
    }
}
