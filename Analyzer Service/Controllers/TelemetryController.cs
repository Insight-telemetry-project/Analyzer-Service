using Analyzer_Service.Models.Schema;
using Analyzer_Service.Services.Mongo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Analyzer_Service.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TelemetryController : ControllerBase
    {
        private readonly FlightTelemetryMongoProxy _telemetryService;

        public TelemetryController(FlightTelemetryMongoProxy telemetryService)
        {
            _telemetryService = telemetryService;
        }

        [HttpGet("fields/{masterIndex}")]
        public async Task<IActionResult> GetFieldsByMasterIndex(int masterIndex)
        {
            List<TelemetrySensorFields> result = await _telemetryService.GetFromFieldsAsync(masterIndex);
            if (result.Count == 0)
            {
                return NotFound($"No TelemetryFields found for Master Index {masterIndex}");
            }
            return Ok(result);
        }

        [HttpGet("flight/{masterIndex}")]
        public async Task<IActionResult> GetFlightByMasterIndex(int masterIndex)
        {
            List<TelemetryFlightData> result = await _telemetryService.GetFromFlightDataAsync(masterIndex);
            if (result.Count == 0)
            {
                return NotFound($"No TelemetryFlightData found for Master Index {masterIndex}");
            }
            return Ok(result);
        }
    }
}
