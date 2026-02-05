using Analyzer_Service.Models.Dto;

namespace Analyzer_Service.Models.Interface.Algorithms.Pelt
{
    public interface IFlightPhaseAnalysisService
    {
        Task<FlightPhaseIndexes> GetPhaseIndexesAsync(int flightId, string fieldName);
    }
}
