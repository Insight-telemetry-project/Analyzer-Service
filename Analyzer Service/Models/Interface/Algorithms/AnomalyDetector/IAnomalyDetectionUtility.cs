using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Enums;

namespace Analyzer_Service.Models.Interface.Algorithms.AnomalyDetector
{
    public interface IAnomalyDetectionUtility
    {
        List<int> DetectAnomalies(
            double[] processedSignalValues,
            IReadOnlyList<SegmentBoundary> segmentBoundaries,
            IReadOnlyList<SegmentClassificationResult> classificationResults,
            IReadOnlyList<SegmentFeatures> featureValueList,
            flightStatus status);
    }
}
