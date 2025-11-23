namespace Analyzer_Service.Models.Ro.Algorithms
{
    public class HistoricalSimilarityResult
    {
        public int MasterIndex { get; set; }
        public int SegmentIndex { get; set; }
        public double FinalScore { get; set; }
        public double HashSimilarity { get; set; }
        public double FeatureSimilarity { get; set; }
        public double DurationSimilarity { get; set; }
    }
}
