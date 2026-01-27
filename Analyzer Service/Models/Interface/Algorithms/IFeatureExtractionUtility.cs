using Analyzer_Service.Models.Algorithms;
using Analyzer_Service.Models.Dto;

namespace Analyzer_Service.Models.Interface.Algorithms
{
    public interface IFeatureExtractionUtility
    {
        List<SegmentBoundary> BuildSegmentsFromPoints(List<int> boundaries, int sampleCount);

        SegmentFeatures ExtractFeatures(
            double[] timeSeriesValues,
            double[] processedSignalValues,
            SegmentBoundary segmentBoundary,
            double previousMean,
            double nextMean);

        int CountPeaks(double[] signalValues, int startIndex, int endIndex);

        int CountTroughs(double[] signalValues, int startIndex, int endIndex);
    }
}
