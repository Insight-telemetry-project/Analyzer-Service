namespace Analyzer_Service.Models.Ro.Algorithms
{
    public class FlightCausalityAnalysisResult
    {
        public int Flight { get; set; }
        public int TotalPairs { get; set; }
        public List<PairCausalityResult> Results { get; set; }
    }

}
