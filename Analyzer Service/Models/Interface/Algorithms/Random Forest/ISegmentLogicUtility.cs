using Analyzer_Service.Models.Dto;

namespace Analyzer_Service.Models.Interface.Algorithms.Random_Forest
{
    public interface ISegmentLogicUtility
    {
        
            List<double> ComputeMeansPerSegment(
                List<double> signalValues,
                List<SegmentBoundary> segmentBoundaries);

            List<SegmentClassificationResult> ClassifySegments(
                List<double> timeSeriesValues,
                List<double> signalValues,
                List<SegmentBoundary> segmentBoundaries,
                List<double> meanValuesPerSegment);

            List<SegmentFeatures> BuildFeatureList(
                List<double> timeSeriesValues,
                List<double> signalValues,
                List<SegmentBoundary> segmentBoundaries,
                List<double> meanValuesPerSegment);

            List<SegmentClassificationResult> MergeSegments(
                List<SegmentClassificationResult> classificationResults);
        
    }
}