using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Mongo;

namespace Analyzer_Service.Services.Algorithms
{
    public class FlightCausality: IFlightCausality
    {
        private readonly IGrangerCausalityAnalyzer _grangerAnalyzer;
        private readonly IPrepareFlightData _flightDataPreparer;

        public FlightCausality(
            IGrangerCausalityAnalyzer grangerAnalyzer,
            IPrepareFlightData flightDataPreparer)
        {
            _grangerAnalyzer = grangerAnalyzer;
            _flightDataPreparer = flightDataPreparer;
        }

        public async Task<object> AnalyzeCausalityAsync(int masterIndex, string xField, string yField, int lag)
        {
            (List<double> xSeries, List<double> ySeries) = await _flightDataPreparer.PrepareFlightDataAsync(masterIndex, xField, yField);

            if (xSeries.Count == 0 || ySeries.Count == 0)
            {
                return new
                {
                    MasterIndex = masterIndex,
                    Message = "No valid data found for the selected fields."
                };
            }

            double xToY = _grangerAnalyzer.ComputeCausality(xSeries, ySeries, lag);
            double yToX = _grangerAnalyzer.ComputeCausality(ySeries, xSeries, lag);

            string relationship = GetRelationshipLabel(xToY, yToX);

            return new
            {
                MasterIndex = masterIndex,
                XField = xField,
                YField = yField,
                LagCount = lag,
                XtoY_ImprovementRatio = xToY,
                YtoX_ImprovementRatio = yToX,
                XcausesY = xToY > ConstantCausality.SIGNIFICANCE_THRESHOLD,
                YcausesX = yToX > ConstantCausality.SIGNIFICANCE_THRESHOLD,
                Relationship = relationship
            };
        }

        private string GetRelationshipLabel(double xToY, double yToX)
        {
            double threshold = ConstantCausality.SIGNIFICANCE_THRESHOLD;
            if (xToY > threshold && yToX > threshold) return "Two-way influence";
            if (xToY > threshold) return "X causes Y";
            if (yToX > threshold) return "Y causes X";
            return "No causality detected";
        }
    }
}
