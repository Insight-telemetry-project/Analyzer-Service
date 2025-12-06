using static Analyzer_Service.Models.Algorithms.Type.Types;

namespace Analyzer_Service.Models.Ro.Algorithms
{
    public class PairCausalityResult
    {
        public int MasterIndex { get; set; }
        public string SourceField { get; set; }
        public string TargetField { get; set; }
        public CausalityAlgorithm SelectedAlgorithm { get; set; }
        public double GrangerValue { get; set; }
        public double CcmValue { get; set; }
    }

}
