using Analyzer_Service.Models.Enums;

namespace Analyzer_Service.Models.Interface.Algorithms.Pelt
{
    public interface IChangePointDetectionService
    {
        List<int> DetectChangePoints(double[] rawSignal,flightStatus status);    
    }
}