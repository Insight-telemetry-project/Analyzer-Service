using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms.Ccm;
using Analyzer_Service.Models.Interface.Mongo;

namespace Analyzer_Service.Services.Algorithms
{
    public class FlightCausality : IFlightCausality
    {
        private readonly IGrangerCausalityAnalyzer _grangerAnalyzer;
        private readonly ICcmCausalityAnalyzer _ccmAnalyzer;
        private readonly IPrepareFlightData _flightDataPreparer;

        public FlightCausality(
            IGrangerCausalityAnalyzer grangerAnalyzer,
            ICcmCausalityAnalyzer ccmAnalyzer,
            IPrepareFlightData flightDataPreparer)
        {
            _grangerAnalyzer = grangerAnalyzer;
            _ccmAnalyzer = ccmAnalyzer;
            _flightDataPreparer = flightDataPreparer;
        }

        public async Task<object> AnalyzeGrangerAsync(int masterIndex, string xField, string yField, int lag)
        {
            (List<double> xSeries, List<double> ySeries) =
                await _flightDataPreparer.PrepareFlightDataAsync(masterIndex, xField, yField);

            if (xSeries.Count == 0 || ySeries.Count == 0)
            {
                return new
                {
                    MasterIndex = masterIndex,
                    Message = "No valid data found for the selected fields."
                };
            }

            double xToYValue = _grangerAnalyzer.ComputeCausality(xSeries, ySeries, lag);
            double yToXValue = _grangerAnalyzer.ComputeCausality(ySeries, xSeries, lag);

            string relationship = GetRelationshipLabel(xToYValue, yToXValue, ConstantCausality.SIGNIFICANCE_THRESHOLD);

            return new
            {
                MasterIndex = masterIndex,
                XField = xField,
                YField = yField,
                LagCount = lag,
                XtoY_ImprovementRatio = xToYValue,
                YtoX_ImprovementRatio = yToXValue,
                XcausesY = xToYValue > ConstantCausality.SIGNIFICANCE_THRESHOLD,
                YcausesX = yToXValue > ConstantCausality.SIGNIFICANCE_THRESHOLD,
                Relationship = relationship
            };
        }

        public async Task<object> AnalyzeCcmAsync(int masterIndex, string xField, string yField, int embeddingDim, int delay)
        {
            (List<double> xSeries, List<double> ySeries) =
                await _flightDataPreparer.PrepareFlightDataAsync(masterIndex, xField, yField);

            if (xSeries.Count == 0 || ySeries.Count == 0)
            {
                return new
                {
                    MasterIndex = masterIndex,
                    Message = "No valid data found for the selected fields."
                };
            }

            double xToYCorrelation = _ccmAnalyzer.ComputeCausality(xSeries, ySeries, embeddingDim, delay);
            double yToXCorrelation = _ccmAnalyzer.ComputeCausality(ySeries, xSeries, embeddingDim, delay);

            string relationship = GetRelationshipLabel(xToYCorrelation, yToXCorrelation, 0.3);

            return new
            {
                MasterIndex = masterIndex,
                XField = xField,
                YField = yField,
                EmbeddingDimension = embeddingDim,
                TimeDelay = delay,
                XtoY_Correlation = xToYCorrelation,
                YtoX_Correlation = yToXCorrelation,
                Relationship = relationship
            };
        }

        private string GetRelationshipLabel(double xToY, double yToX, double threshold)
        {
            if (xToY > threshold && yToX > threshold)
                return "Two-way influence";
            if (xToY > threshold)
                return "X causes Y";
            if (yToX > threshold)
                return "Y causes X";
            return "No causality detected";
        }
    }
}
