namespace Analyzer_Service.Models.Interface.Algorithms.Pelt
{
    namespace Analyzer_Service.Models.Interface.Algorithms.Pelt
    {
        public interface IPeltAlgorithm
        {
            IReadOnlyList<int> DetectChangePoints(
                IReadOnlyList<double> values,
                int minSegmentSamples,
                int jump,
                double penaltyBeta);
        }
    }

}
