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
using MongoDB.Driver;

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
            IAsyncCursor<TelemetrySensorFields> cursor =
                await _flightTelemetryMongoProxy.GetCursorFromFieldsAsync(masterIndex);

            List<TelemetrySensorFields> list = new List<TelemetrySensorFields>();

            await foreach (TelemetrySensorFields record in cursor.ToAsyncEnumerable())
            {
                list.Add(record);
            }

            if (list.Count == 0)
            {
                return NotFound($"No TelemetryFields found for Master Index {masterIndex}");
            }

            return Ok(list);
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
            var result = await _segmentClassifier.ClassifyWithAnomaliesAsync(masterIndex, fieldName,0,0);

            return Ok(new
            {
                segments = result.Segments,
                anomalies = result.Anomalies
            });
        }

        [HttpGet("segments-with-anomalies/{masterIndex}/{fieldName}/{startIndex}/{endIndex}")]
        public async Task<IActionResult> ClassifyWithRange(int masterIndex,string fieldName,int startIndex,int endIndex){
            var result = await _segmentClassifier.ClassifyWithAnomaliesAsync(
            masterIndex,
            fieldName,
            startIndex,
            endIndex);

            return Ok(new
            {
                segments = result.Segments,
                anomalies = result.Anomalies
            });
        }

    }
}


