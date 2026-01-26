using Analyzer_Service.Models.Algorithms;
using Analyzer_Service.Models.Dto;

namespace Analyzer_Service.Models.Interface.Algorithms
{
    public interface IFeatureExtractionUtility
    {
        public List<SegmentBoundary> BuildSegmentsFromPoints(List<int> boundaries,int sampleCount);

        SegmentFeatures ExtractFeatures(List<double> timeSeries, List<double> processedSignal, SegmentBoundary segment, double previousMean, double nextMean);


        public int CountPeaks(List<double> signal,int startIndex,int endIndex);

        public int CountTroughs(List<double> signal,int startIndex,int endIndex);
    }
}
