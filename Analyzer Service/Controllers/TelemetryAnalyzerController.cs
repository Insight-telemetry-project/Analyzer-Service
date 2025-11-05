using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Models.Schema;
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

        public TelemetryAnalyzerController(IFlightTelemetryMongoProxy flightTelemetryMongoProxy, IGrangerCausalityAnalyzer grangerCausalityAnalyzer,
            IPrepareFlightData flightDataPreparer, IFlightCausality flightCausalityService)
        {
            _flightTelemetryMongoProxy = flightTelemetryMongoProxy;
            _grangerAnalyzer = grangerCausalityAnalyzer;
            _flightDataPreparer = flightDataPreparer;
            _flightCausality = flightCausalityService;



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


        [HttpGet("analyze/{masterIndex}/{xField}/{yField}/{lag}")]
        public async Task<IActionResult> Analyze(int masterIndex, string xField, string yField, int lag)
        {
            object result = await _flightCausality.AnalyzeCausalityAsync(masterIndex, xField, yField, lag);
            return Ok(result);
        }
    }
}

