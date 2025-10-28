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
        public async Task<object> AnalyzeHybridAsync(int masterIndex, string xField, string yField, int lag, int embeddingDim, int delay)
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

            double grangerForward = _grangerAnalyzer.ComputeCausality(xSeries, ySeries, lag);
            double grangerReverse = _grangerAnalyzer.ComputeCausality(ySeries, xSeries, lag);
            double ccmForward = _ccmAnalyzer.ComputeCausality(xSeries, ySeries, embeddingDim, delay);
            double ccmReverse = _ccmAnalyzer.ComputeCausality(ySeries, xSeries, embeddingDim, delay);

            double normGrangerForward = Math.Clamp(grangerForward, 0.0, 1.0);
            double normGrangerReverse = Math.Clamp(grangerReverse, 0.0, 1.0);
            double normCcmForward = Math.Clamp(ccmForward, 0.0, 1.0);
            double normCcmReverse = Math.Clamp(ccmReverse, 0.0, 1.0);

            double alpha = 0.5;
            double hybridForward = alpha * normGrangerForward + (1 - alpha) * normCcmForward;
            double hybridReverse = alpha * normGrangerReverse + (1 - alpha) * normCcmReverse;

            string relationship = GetRelationshipLabel(hybridForward, hybridReverse, 0.3);

            return new
            {
                MasterIndex = masterIndex,
                XField = xField,
                YField = yField,
                Lag = lag,
                EmbeddingDimension = embeddingDim,
                TimeDelay = delay,
                GrangerForward = grangerForward,
                GrangerReverse = grangerReverse,
                CcmForward = ccmForward,
                CcmReverse = ccmReverse,
                HybridForward = hybridForward,
                HybridReverse = hybridReverse,
                Relationship = relationship
            };
        }

    }
}
