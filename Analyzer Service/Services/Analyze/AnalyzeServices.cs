using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Enums;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms.Clustering;
using Analyzer_Service.Models.Interface.Algorithms.HistoricalAnomaly;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Analyze;
using Analyzer_Service.Models.Ro.Algorithms;

namespace Analyzer_Service.Services.Analyze
{
    public class AnalyzeServices: IAnalyzeServices
    {
        private readonly IFlightCausality _flightCausality;
        private readonly ISegmentClassificationService _segmentClassifier;
        private readonly IHistoricalAnomalySimilarityService _historicalSimilarityService;
        private readonly IFlightPhaseAnalysisService _phaseAnalysis;
        private readonly IFlightPhaseDetector _flightPhaseDetector;


        public AnalyzeServices(
            IFlightCausality flightCausalityService,
            ISegmentClassificationService segmentClassifier,
            IHistoricalAnomalySimilarityService historicalSimilarityService,
            IFlightPhaseDetector flightPhaseDetector,
            IFlightPhaseAnalysisService phaseAnalysis
)
        {
            _flightCausality = flightCausalityService;
            _segmentClassifier = segmentClassifier;
            _historicalSimilarityService = historicalSimilarityService;
            _flightPhaseDetector = flightPhaseDetector;
            _phaseAnalysis = phaseAnalysis;

        }

        public async Task Analyze(int flightId) { 
            
            foreach (string fieldName in ParamterList.ParameterNames)
            {
                FlightPhaseIndexes phaseIndexes = await _phaseAnalysis.GetPhaseIndexesAsync(flightId, fieldName);

                int takeoffEndIndex = phaseIndexes.TakeoffEndIndex;
                int landingStartIndex = phaseIndexes.LandingStartIndex;



                SegmentAnalysisResult takeoff =
                    await _segmentClassifier.ClassifyWithAnomaliesAsync(flightId, fieldName, 0, takeoffEndIndex, flightStatus.TakeOf_Landing);

                SegmentAnalysisResult cruise =
                    await _segmentClassifier.ClassifyWithAnomaliesAsync(flightId, fieldName, takeoffEndIndex, landingStartIndex, flightStatus.Cruising);

                SegmentAnalysisResult landing =
                    await _segmentClassifier.ClassifyWithAnomaliesAsync(flightId, fieldName, landingStartIndex, int.MaxValue, flightStatus.TakeOf_Landing);

            }
            foreach (string fieldName in ParamterList.ParameterNames) {
                List<HistoricalSimilarityResult> results = await _historicalSimilarityService.FindSimilarAnomaliesAsync(flightId, fieldName, flightStatus.FullFlight);
            }
            FlightCausalityAnalysisResult result = await _flightCausality.AnalyzeFlightAsync(flightId);
        }
    }
}
