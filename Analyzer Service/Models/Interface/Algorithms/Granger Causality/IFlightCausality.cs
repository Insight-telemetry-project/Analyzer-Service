using Analyzer_Service.Models.Ro.Algorithms;

namespace Analyzer_Service.Models.Interface.Algorithms
{
    public interface IFlightCausality
    {
        Task<FlightCausalityAnalysisResult> AnalyzeFlightAsync(int masterIndex);

    }
}
