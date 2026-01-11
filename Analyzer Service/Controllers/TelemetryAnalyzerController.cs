using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms.Ccm;
using Analyzer_Service.Models.Interface.Algorithms.Clustering;
using Analyzer_Service.Models.Interface.Algorithms.HistoricalAnomaly;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Models.Ro.Algorithms;
using Analyzer_Service.Models.Schema;
using Analyzer_Service.Services;
using Analyzer_Service.Services.Algorithms.Pelt;
using Analyzer_Service.Services.Mongo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

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
        public async Task<IActionResult> AnalyzeFlightSegments(int flightId,string fieldName,int? startIndex = null,int? endIndex = null)
        {
            int start = startIndex ?? 0;
            int end = endIndex ?? 0;

            SegmentAnalysisResult result = await _segmentClassifier.ClassifyWithAnomaliesAsync(flightId,fieldName,start,end);

            return Ok(result);
        }



        [HttpGet("similar-anomalies/{flightId}/{fieldName}")]
        public async Task<IActionResult> FindSimilarAnomalies(int flightId, string fieldName)
        {
            List<HistoricalSimilarityResult> results =await _historicalSimilarityService.FindSimilarAnomaliesAsync(flightId, fieldName);

            return Ok(results);
        }


        [HttpGet("segments-with-anomalies-phases/{flightId}/{fieldName}")]
        public async Task<IActionResult> AnalyzeFlightSegmentsByPhases(int flightId, string fieldName)
        {
            SegmentAnalysisResult full = await _segmentClassifier.ClassifyWithAnomaliesAsync(flightId, fieldName, 0, 0);

            FlightPhaseIndexes phaseIndexes = _flightPhaseDetector.Detect(full);

            Task<SegmentAnalysisResult> takeoffTask =
                _segmentClassifier.ClassifyWithAnomaliesAsync(flightId, fieldName, 0, phaseIndexes.TakeoffEndIndex);

            Task<SegmentAnalysisResult> cruiseTask =
                _segmentClassifier.ClassifyWithAnomaliesAsync(flightId, fieldName, phaseIndexes.TakeoffEndIndex, phaseIndexes.LandingStartIndex);

            Task<SegmentAnalysisResult> landingTask =
                _segmentClassifier.ClassifyWithAnomaliesAsync(flightId, fieldName, phaseIndexes.LandingStartIndex, 0);

            await Task.WhenAll(takeoffTask, cruiseTask, landingTask);

            return Ok(new
            {
                takeoffEndIndex = phaseIndexes.TakeoffEndIndex,
                landingStartIndex = phaseIndexes.LandingStartIndex,
                takeoff = takeoffTask.Result,
                cruise = cruiseTask.Result,
                landing = landingTask.Result
            });
        }



    }
}


