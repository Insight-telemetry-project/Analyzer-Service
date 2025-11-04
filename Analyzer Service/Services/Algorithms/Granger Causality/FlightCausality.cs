using Analyzer_Service.Models.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms.Ccm;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Models.Ro.Algorithms;
using Analyzer_Service.Models.Schema;
using System.Collections.Concurrent;

namespace Analyzer_Service.Services.Algorithms
{
    public class FlightCausality : IFlightCausality
    {
        private readonly IAutoCausalitySelector _autoSelector;
        private readonly IGrangerCausalityAnalyzer _grangerAnalyzer;
        private readonly ICcmCausalityAnalyzer _ccmAnalyzer;
        private readonly IFlightTelemetryMongoProxy _flightTelemetryMongoProxy;

        public FlightCausality(
            IGrangerCausalityAnalyzer grangerAnalyzer,
            ICcmCausalityAnalyzer ccmAnalyzer,
            IAutoCausalitySelector autoSelector,
            IFlightTelemetryMongoProxy flightTelemetryMongoProxy)
        {
            _grangerAnalyzer = grangerAnalyzer;
            _ccmAnalyzer = ccmAnalyzer;
            _autoSelector = autoSelector;
            _flightTelemetryMongoProxy = flightTelemetryMongoProxy;
        }

        public async Task<object> AnalyzeFlightAsync(int masterIndex, int lag, int embeddingDim, int delay)
        {
            List<TelemetrySensorFields> flightData = await _flightTelemetryMongoProxy.GetFromFieldsAsync(masterIndex);
            if (flightData.Count == 0)
                return new { Message = $"No data found for flight {masterIndex}" };

            Dictionary<string, List<double>> allFields = new();
            foreach (var record in flightData)
            {
                foreach (var kvp in record.Fields)
                {
                    if (!allFields.ContainsKey(kvp.Key))
                        allFields[kvp.Key] = new List<double>();
                    allFields[kvp.Key].Add(kvp.Value);
                }
            }

            List<string> fieldNames = allFields.Keys.ToList();
            List<(string X, string Y)> pairs = new();
            for (int i = 0; i < fieldNames.Count; i++)
            {
                for (int j = 0; j < fieldNames.Count; j++)
                {
                    if (i != j)
                        pairs.Add((fieldNames[i], fieldNames[j]));
                }
            }

            ConcurrentBag<object> results = new();

            await Parallel.ForEachAsync(pairs, async (pair, token) =>
            {
                object result = await AnalyzePairAsync(masterIndex, pair.X, pair.Y, lag, embeddingDim, delay, allFields);
                results.Add(result);
            });

            return new
            {
                Flight = masterIndex,
                TotalPairs = pairs.Count,
                Results = results.ToList()
            };
        }

        private async Task<object> AnalyzePairAsync(
            int masterIndex,
            string xField,
            string yField,
            int lag,
            int embeddingDim,
            int delay,
            Dictionary<string, List<double>> allFields)
        {
            List<double> source = allFields.ContainsKey(xField) ? allFields[xField] : new List<double>();
            List<double> target = allFields.ContainsKey(yField) ? allFields[yField] : new List<double>();

            if (source.Count == 0 || target.Count == 0)
            {
                return new
                {
                    MasterIndex = masterIndex,
                    XField = xField,
                    YField = yField,
                    Message = "Missing field data"
                };
            }

            CausalitySelectionResult causalitySelectionResult = _autoSelector.SelectAlgorithm(source, target);
            string selected = causalitySelectionResult.SelectedAlgorithm;
            string reason = causalitySelectionResult.Reasoning;

            double grangerValue = 0.0;
            double ccmValue = 0.0;

            if (selected == "Granger")
                grangerValue = _grangerAnalyzer.ComputeCausality(source, target, lag);

            if (selected == "CCM")
                ccmValue = _ccmAnalyzer.ComputeCausality(source, target, embeddingDim, delay);

            if (grangerValue > 0.05 || ccmValue > 0.1)
                await _flightTelemetryMongoProxy.StoreConnectionsAsync(masterIndex, xField, yField);

            return new
            {
                MasterIndex = masterIndex,
                XField = xField,
                YField = yField,
                SelectedAlgorithm = selected,
                Reasoning = reason,
                GrangerValue = grangerValue,
                CcmValue = ccmValue
            };
        }
    }
}
