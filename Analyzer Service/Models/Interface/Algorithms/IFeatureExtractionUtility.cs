namespace Analyzer_Service.Models.Interface.Algorithms
{
    public interface IFeatureExtractionUtility
    {
        double[] ExtractFeatures(
            IReadOnlyList<double> timeSeries,
            IReadOnlyList<double> processedSignal,
            int startIndex,
            int endIndex,
            double previousMean,
            double nextMean);

        List<(int StartIndex, int EndIndex)> BuildSegments(
            List<int> boundaries,
            int sampleCount);

        int CountPeaks(
            IReadOnlyList<double> signal,
            int startIndex,
            int endIndex);

        int CountTroughs(
            IReadOnlyList<double> signal,
            int startIndex,
            int endIndex);
    }
}
