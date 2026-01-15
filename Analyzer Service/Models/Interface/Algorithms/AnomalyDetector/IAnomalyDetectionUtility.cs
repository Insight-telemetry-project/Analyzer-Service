using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Enums;

namespace Analyzer_Service.Models.Interface.Algorithms.AnomalyDetector
{
    public interface IAnomalyDetectionUtility
    {
        public List<int> DetectAnomalies(
            List<double> timeSeries,
            List<double> processedSignal,
            List<SegmentBoundary> segmentList,
            List<string> labelList,
            List<SegmentFeatures> featureList,
            flightStatus status);
    }
}
