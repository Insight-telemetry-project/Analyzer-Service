using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Enums;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms.Clustering;
using Analyzer_Service.Models.Interface.Algorithms.HistoricalAnomaly;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.ControllerUtils;
using Analyzer_Service.Models.Ro.Algorithms;
using Microsoft.AspNetCore.Mvc;
namespace Analyzer_Service.Services.ControllerUtils
{
    public class ControllerUtils: IControllerUtils
    {


        private readonly IFlightCausality _flightCausality;
        private readonly ISegmentClassificationService _segmentClassifier;
        private readonly IHistoricalAnomalySimilarityService _historicalSimilarityService;
        private readonly IFlightPhaseDetector _flightPhaseDetector;


        public ControllerUtils(
            IFlightCausality flightCausalityService,
            ISegmentClassificationService segmentClassifier,
            IHistoricalAnomalySimilarityService historicalSimilarityService,
            IFlightPhaseDetector flightPhaseDetector

)
        {
            _flightCausality = flightCausalityService;
            _segmentClassifier = segmentClassifier;
            _historicalSimilarityService = historicalSimilarityService;
            _flightPhaseDetector = flightPhaseDetector;
        }


        public async Task AnalyzeFlightSegmentsByPhases(int flightId, string fieldName)
        {
            SegmentAnalysisResult full =
                await _segmentClassifier.ClassifyWithAnomaliesAsync(flightId, fieldName, 0, 0, flightStatus.FullFlight);

            FlightPhaseIndexes phaseIndexes = _flightPhaseDetector.Detect(full);

            int takeoffEndIndex = phaseIndexes.TakeoffEndIndex;
            int landingStartIndex = phaseIndexes.LandingStartIndex;

            SegmentAnalysisResult takeoff =
                await _segmentClassifier.ClassifyWithAnomaliesAsync(flightId, fieldName, 0, takeoffEndIndex, flightStatus.TakeOf_Landing);

            SegmentAnalysisResult cruise =
                await _segmentClassifier.ClassifyWithAnomaliesAsync(flightId, fieldName, takeoffEndIndex, landingStartIndex, flightStatus.Cruising);

            SegmentAnalysisResult landing =
                await _segmentClassifier.ClassifyWithAnomaliesAsync(flightId, fieldName, landingStartIndex, int.MaxValue, flightStatus.TakeOf_Landing);
        }
        public async Task AnalyzeFullFlight(int flightId)
        {
            //כמה מקבליות אפשר להכניס כאן?
           foreach (string field in FlightParameter.flightParameters)
           {
                await AnalyzeFlightSegmentsByPhases(flightId, field);
           }
            foreach (string field in FlightParameter.flightParameters)
            {
                await _historicalSimilarityService.FindSimilarAnomaliesAsync(flightId, field, flightStatus.FullFlight);
            }
            await _flightCausality.AnalyzeFlightAsync(flightId);

        }
    }
}
