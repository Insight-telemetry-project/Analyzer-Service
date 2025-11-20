using Analyzer_Service.Models.Dto;

namespace Analyzer_Service.Models.Interface.Algorithms.AnomalyDetector
{
    public interface IAnomalyDetectionUtility
    {
        List<int> DetectAnomalies(
            List<double> timeSeries,
            List<double> processedSignal,
            List<SegmentBoundary> segmentList,
            List<string> labelList,
            List<Dictionary<string, double>> featureList);
    }
}
