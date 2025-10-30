using Analyzer_Service.Models.Algorithms;
using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms.Ccm;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Models.Ro.Algorithms;

namespace Analyzer_Service.Services.Algorithms
{
    public class FlightCausality : IFlightCausality
    {
        private readonly IAutoCausalitySelector _autoSelector;
        private readonly IGrangerCausalityAnalyzer _grangerAnalyzer;
        private readonly ICcmCausalityAnalyzer _ccmAnalyzer;
        private readonly IPrepareFlightData _flightDataPreparer;
        private readonly IFlightTelemetryMongoProxy _flightTelemetryMongoProxy;

        public FlightCausality(
            IGrangerCausalityAnalyzer grangerAnalyzer,
            ICcmCausalityAnalyzer ccmAnalyzer,
            IPrepareFlightData flightDataPreparer,
            IAutoCausalitySelector autoSelector,
            IFlightTelemetryMongoProxy flightTelemetryMongoProxy)
        {
            _grangerAnalyzer = grangerAnalyzer;
            _ccmAnalyzer = ccmAnalyzer;
            _flightDataPreparer = flightDataPreparer;
            _autoSelector = autoSelector;
            _flightTelemetryMongoProxy = flightTelemetryMongoProxy;
        }

        public async Task<object> AnalyzeAutoAsync(int masterIndex, string xField, string yField, int lag, int embeddingDim, int delay)
        {
            (List<double> source, List<double> target) =
                await _flightDataPreparer.PrepareFlightDataAsync(masterIndex, xField, yField);

            CausalitySelectionResult causalitySelectionResult = _autoSelector.SelectAlgorithm(source, target);
            string selected = causalitySelectionResult.SelectedAlgorithm;
            string reason = causalitySelectionResult.Reasoning;

            double grangerValue = 0.0;
            double ccmValue = 0.0;

            if (selected == "Granger")
                grangerValue = _grangerAnalyzer.ComputeCausality(source, target, lag);

            if (selected == "CCM")
                ccmValue = _ccmAnalyzer.ComputeCausality(source, target, embeddingDim, delay);

            if (grangerValue > 0.01 || ccmValue > 0.01)
            {
                await _flightTelemetryMongoProxy.StoreConnectionsAsync(masterIndex, xField, yField);
            }

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
