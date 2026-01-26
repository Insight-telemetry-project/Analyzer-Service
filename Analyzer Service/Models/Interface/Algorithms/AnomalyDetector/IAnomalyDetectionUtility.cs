using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Enums;

namespace Analyzer_Service.Models.Interface.Algorithms.AnomalyDetector
{
    public interface IAnomalyDetectionUtility
    {
        List<int> DetectAnomalies(
    List<double> processedSignalValues,
    List<SegmentBoundary> segmentBoundaries,
    List<string> segmentLabelList,
    List<SegmentFeatures> featureValueList,
    flightStatus status);

    }
}
