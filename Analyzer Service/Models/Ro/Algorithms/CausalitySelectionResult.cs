namespace Analyzer_Service.Models.Ro.Algorithms
{
    public class CausalitySelectionResult
    {
        public string SelectedAlgorithm { get; }
        public string Reasoning { get; }
        public double PearsonCorrelation { get; }
        public double DerivativeCorrelation { get; }

        public CausalitySelectionResult(string selectedAlgorithm, string reasoning, double pearson, double derivative)
        {
            SelectedAlgorithm = selectedAlgorithm;
            Reasoning = reasoning;
            PearsonCorrelation = pearson;
            DerivativeCorrelation = derivative;
        }
    }
}
