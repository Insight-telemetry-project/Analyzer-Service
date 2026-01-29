namespace Analyzer_Service.Models.Ro.Algorithms
{
    public class HistoricalSimilarityResult
    {
        public int MasterIndex { get; set; }

        public int HistoricalStartIndex { get; set; }
        public int HistoricalEndIndex { get; set; }
        public string HistoricalLabel { get; set; }

        public int NewStartIndex { get; set; }
        public int NewEndIndex { get; set; }
        public string NewLabel { get; set; }

        public double FinalScore { get; set; }
        public double HashSimilarity { get; set; }
        public double FeatureSimilarity { get; set; }
        public double DurationSimilarity { get; set; }
    }

}
