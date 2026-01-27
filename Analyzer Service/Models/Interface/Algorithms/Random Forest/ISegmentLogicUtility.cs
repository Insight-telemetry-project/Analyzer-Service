using Analyzer_Service.Models.Dto;

namespace Analyzer_Service.Models.Interface.Algorithms.Random_Forest
{
    public interface ISegmentLogicUtility
    {
        List<double> ComputeMeansPerSegment(double[] signalValues, List<SegmentBoundary> segmentBoundaries);

        List<SegmentClassificationResult> ClassifySegments(
            double[] timeSeriesValues,
            double[] signalValues,
            List<SegmentBoundary> segmentBoundaries,
            List<double> meanValuesPerSegment);

        List<SegmentClassificationResult> MergeSegments(List<SegmentClassificationResult> segments);

        List<SegmentFeatures> BuildFeatureList(
            double[] timeSeriesValues,
            double[] signalValues,
            List<SegmentBoundary> segmentBoundaries,
            List<double> meanValuesPerSegment);
    }
}