namespace Analyzer_Service.Models.Dto
{
    public class SimilarityScores
    {
        public double FinalScore { get; set; }
        public double HashSimilarity { get; set; }
        public double FeatureSimilarity { get; set; }
        public double DurationSimilarity { get; set; }
    }

}
