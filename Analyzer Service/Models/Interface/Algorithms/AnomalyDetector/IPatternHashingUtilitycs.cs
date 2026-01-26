using Analyzer_Service.Models.Dto;

namespace Analyzer_Service.Models.Interface.Algorithms.AnomalyDetector
{
    public interface IPatternHashingUtility
    {
        string ComputeHash(List<double> processedSignal, SegmentBoundary segmentBoundary);

    }
}
