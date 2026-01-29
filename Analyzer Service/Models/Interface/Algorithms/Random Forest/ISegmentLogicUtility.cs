using Analyzer_Service.Models.Dto;

namespace Analyzer_Service.Models.Interface.Algorithms.Random_Forest
{
    public interface ISegmentLogicUtility
    {
        double[] ComputeMeansPerSegment(double[] signalValues, List<SegmentBoundary> segmentBoundaries);

        List<SegmentClassificationResult> ClassifySegments(
            double[] signalValues,
            List<SegmentBoundary> segmentBoundaries,
            double[] meanValuesPerSegment);

        List<SegmentClassificationResult> MergeSegments(List<SegmentClassificationResult> segments);

        List<SegmentFeatures> BuildFeatureList(
            double[] signalValues,
            List<SegmentBoundary> segmentBoundaries,
            double[] meanValuesPerSegment);
    }
}