using Analyzer_Service.Models.Dto;

namespace Analyzer_Service.Models.Interface.Algorithms.Random_Forest
{
    public interface ISegmentLogicUtility
    {
        public List<double> ComputeMeansPerSegment(List<double> signal, List<SegmentBoundary> segments);
        public List<SegmentClassificationResult> ClassifySegments(List<double> timeSeries,List<double> signal,
            List<SegmentBoundary> segments,
            List<double> meanValues);
        public List<SegmentClassificationResult> MergeSegments(List<SegmentClassificationResult> segments);

        public List<Dictionary<string, double>> BuildFeatureList(List<double> timeSeries,List<double> signal,
            List<SegmentBoundary> segments,List<double> meanValues);
    }
}
