using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms.Ccm;
using Analyzer_Service.Models.Interface.Algorithms.Clustering;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Models.Schema;
using Analyzer_Service.Services;
using Analyzer_Service.Services.Mongo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Analyzer_Service.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TelemetryAnalyzerController : ControllerBase
    {
        private readonly IFlightTelemetryMongoProxy _flightTelemetryMongoProxy;
        private readonly IGrangerCausalityAnalyzer _grangerAnalyzer;
        private readonly IPrepareFlightData _flightDataPreparer;
        private readonly IFlightCausality _flightCausality;
        private readonly ICcmCausalityAnalyzer _ccmAnalyzer;
        private readonly IChangePointDetectionService _changePointDetectionService;
        private readonly ISegmentClassificationService _segmentClassifier;


        public TelemetryAnalyzerController(
            IFlightTelemetryMongoProxy flightTelemetryMongoProxy,
            IGrangerCausalityAnalyzer grangerCausalityAnalyzer,
            IPrepareFlightData flightDataPreparer,
            IFlightCausality flightCausalityService,
            ICcmCausalityAnalyzer ccmAnalyzer,
            IChangePointDetectionService changePointDetectionService,
            ISegmentClassificationService segmentClassifier
)
        {
            _flightTelemetryMongoProxy = flightTelemetryMongoProxy;
            _grangerAnalyzer = grangerCausalityAnalyzer;
            _flightDataPreparer = flightDataPreparer;
            _flightCausality = flightCausalityService;
            _ccmAnalyzer = ccmAnalyzer;
            _changePointDetectionService = changePointDetectionService;
            _segmentClassifier = segmentClassifier;

        }

        [HttpGet("fields/{masterIndex}")]
        public async Task<IActionResult> GetFieldsByMasterIndex(int masterIndex)
        {
            List<TelemetrySensorFields> result = await _flightTelemetryMongoProxy.GetFromFieldsAsync(masterIndex);
            if (result.Count == 0)
            {
                return NotFound($"No TelemetryFields found for Master Index {masterIndex}");
            }
            return Ok(result);
        }

        [HttpGet("flight/{masterIndex}")]
        public async Task<IActionResult> GetFlightByMasterIndex(int masterIndex)
        {
            List<TelemetryFlightData> result = await _flightTelemetryMongoProxy.GetFromFlightDataAsync(masterIndex);
            if (result.Count == 0)
            {
                return NotFound($"No TelemetryFlightData found for Master Index {masterIndex}");
            }
            return Ok(result);
        }


        [HttpGet("analyze-flight/{masterIndex}")]
        public async Task<IActionResult> AnalyzeFlight(int masterIndex)
        {
            object result = await _flightCausality.AnalyzeFlightAsync(masterIndex);
            return Ok(result);
        }

        [HttpGet("change-points/{masterIndex}/{fieldName}")]
        public async Task<IActionResult> GetChangePoints(int masterIndex, string fieldName)
        {
            return Ok(await _changePointDetectionService.DetectChangePointsAsync(masterIndex, fieldName));
        }

        [HttpGet("segments/{masterIndex}/{fieldName}")]
        public async Task<IActionResult> GetSegmentsWithLabels(int masterIndex, string fieldName)
        {
            List<SegmentClassificationResult> result = await _segmentClassifier.ClassifyAsync(masterIndex, fieldName);
            return Ok(result);
        }
        [HttpGet("segments-with-anomalies/{masterIndex}/{fieldName}")]
        public async Task<IActionResult> GetSegmentsWithAnomalies(int masterIndex, string fieldName)
        {
            var result = await _segmentClassifier.ClassifyWithAnomaliesAsync(masterIndex, fieldName);

            return Ok(new
            {
                segments = result.Segments,
                anomalies = result.Anomalies
            });
        }

    }
}


