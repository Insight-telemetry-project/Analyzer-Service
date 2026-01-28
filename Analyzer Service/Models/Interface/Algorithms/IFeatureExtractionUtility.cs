using Analyzer_Service.Models.Algorithms;
using Analyzer_Service.Models.Dto;

namespace Analyzer_Service.Models.Interface.Algorithms
{
    public interface IFeatureExtractionUtility
    {
        List<SegmentBoundary> BuildSegmentsFromPoints(List<int> boundaries, int sampleCount);

        SegmentFeatures ExtractFeatures(
            IReadOnlyList<double> processedSignalValues,
            SegmentBoundary segmentBoundary,
            double previousMean,
            double nextMean);

        int CountPeaks(IReadOnlyList<double> signalValues, int startIndex, int endIndex);

        int CountTroughs(IReadOnlyList<double> signalValues, int startIndex, int endIndex);
    }
}
