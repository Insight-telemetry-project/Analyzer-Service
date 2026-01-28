using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Enums;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms.Clustering;
using Analyzer_Service.Models.Interface.Algorithms.HistoricalAnomaly;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Ro.Algorithms;
using Microsoft.AspNetCore.Mvc;

namespace Analyzer_Service.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TelemetryAnalyzerController : ControllerBase
    {
        private readonly IFlightCausality _flightCausality;
        private readonly ISegmentClassificationService _segmentClassifier;
        private readonly IHistoricalAnomalySimilarityService _historicalSimilarityService;


        private readonly IFlightPhaseDetector _flightPhaseDetector;


        public TelemetryAnalyzerController(
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



        [HttpGet("causality-analysis/{flightId}")]
        public async Task<IActionResult> AnalyzeFlightCausalityById(int flightId)
        {
            FlightCausalityAnalysisResult result = await _flightCausality.AnalyzeFlightAsync(flightId);
            return Ok(result);
        }


        [HttpGet("segments-with-anomalies/{flightId}/{fieldName}")]
        public async Task<IActionResult> AnalyzeFlightSegments(int flightId, string fieldName, int? startIndex = null, int? endIndex = null)
        {
            int start = startIndex ?? 0;
            int end = endIndex ?? 0;

            SegmentAnalysisResult result = await _segmentClassifier.ClassifyWithAnomaliesAsync(flightId, fieldName, start, end,flightStatus.FullFlight);

            return Ok(result);
        }



        [HttpGet("similar-anomalies/{flightId}/{fieldName}")]
        public async Task<IActionResult> FindSimilarAnomalies(int flightId, string fieldName)
        {
            List<HistoricalSimilarityResult> results = await _historicalSimilarityService.FindSimilarAnomaliesAsync(flightId, fieldName,flightStatus.FullFlight);

            return Ok(results);
        }


        [HttpGet("segments-with-anomalies-phases/{flightId}/{fieldName}")]
        public async Task<IActionResult> AnalyzeFlightSegmentsByPhases(int flightId, string fieldName)
        {
            SegmentAnalysisResult full =
                await _segmentClassifier.ClassifyWithAnomaliesAsync(flightId, fieldName, 0, 0,flightStatus.FullFlight);

            FlightPhaseIndexes phaseIndexes = _flightPhaseDetector.Detect(full);

            int takeoffEndIndex = phaseIndexes.TakeoffEndIndex;
            int landingStartIndex = phaseIndexes.LandingStartIndex;

            SegmentAnalysisResult takeoff =
                await _segmentClassifier.ClassifyWithAnomaliesAsync(flightId, fieldName, 0, takeoffEndIndex, flightStatus.TakeOf_Landing);

            SegmentAnalysisResult cruise =
                await _segmentClassifier.ClassifyWithAnomaliesAsync(flightId, fieldName, takeoffEndIndex, landingStartIndex, flightStatus.Cruising);

            SegmentAnalysisResult landing =
                await _segmentClassifier.ClassifyWithAnomaliesAsync(flightId, fieldName, landingStartIndex, int.MaxValue, flightStatus.TakeOf_Landing);

            return Ok(new
            {
                takeoffEndIndex,
                landingStartIndex,
                takeoff,
                cruise,
                landing
            });
        }
    }
}


