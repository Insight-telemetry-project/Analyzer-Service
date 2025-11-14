using static Analyzer_Service.Models.Algorithms.Type.Types;

namespace Analyzer_Service.Models.Ro.Algorithms
{
    public class CausalitySelectionResult
    {
        public CausalityAlgorithm SelectedAlgorithm { get; }
        public double PearsonCorrelation { get; }
        public double DerivativeCorrelation { get; }

        public CausalitySelectionResult(
            CausalityAlgorithm selectedAlgorithm,
            double pearsonCorrelation,
            double derivativeCorrelation)
        {
            SelectedAlgorithm = selectedAlgorithm;
            PearsonCorrelation = pearsonCorrelation;
            DerivativeCorrelation = derivativeCorrelation;
        }
    }

}
