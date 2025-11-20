using Analyzer_Service.Models.Algorithms;
using Analyzer_Service.Models.Dto;

namespace Analyzer_Service.Models.Interface.Algorithms
{
    public interface IFeatureExtractionUtility
    {
        List<SegmentBoundary> BuildSegmentsFromPoints(
            List<int> boundaries,
            int sampleCount);

        double[] ExtractFeatures(
    List<double> timeSeries,
    List<double> processedSignal,
    SegmentBoundary segment,
    double previousMean,
    double nextMean);


        int CountPeaks(
            List<double> signal,
            int startIndex,
            int endIndex);

        int CountTroughs(
            List<double> signal,
            int startIndex,
            int endIndex);
    }
}
