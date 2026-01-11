using Analyzer_Service.Models.Dto;
using Analyzer_Service.Services.Algorithms.Pelt;

namespace Analyzer_Service.Models.Interface.Algorithms.Pelt
{
    public interface IFlightPhaseDetector
    {
        FlightPhaseIndexes Detect(SegmentAnalysisResult full);
    }
}
