using Microsoft.AspNetCore.Mvc;

namespace Analyzer_Service.Models.Interface.ControllerUtils
{
    public interface IControllerUtils 
    {
        public Task AnalyzeFlightSegmentsByPhases(int flightId, string fieldName);
        public Task AnalyzeFullFlight(int flightId);

    }
}
