using static Analyzer_Service.Models.Algorithms.Type.Types;

namespace Analyzer_Service.Models.Ro.Algorithms
{
    public class ConnectionResult
    {
        public int MasterIndex { get; }
        public string SourceField { get; }
        public string TargetField { get; }
        public CausalityAlgorithm Algorithm { get; }

        public ConnectionResult(int masterIndex, string sourceField, string targetField, CausalityAlgorithm algorithm)
        {
            MasterIndex = masterIndex;
            SourceField = sourceField;
            TargetField = targetField;
            Algorithm = algorithm;
        }
    }
}
