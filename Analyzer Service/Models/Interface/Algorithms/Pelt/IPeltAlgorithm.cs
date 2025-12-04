namespace Analyzer_Service.Models.Interface.Algorithms.Pelt
{
    namespace Analyzer_Service.Models.Interface.Algorithms.Pelt
    {
        public interface IPeltAlgorithm
        {
            public List<int> DetectChangePoints(List<double> values,int minSegmentSamples,int jump,double penaltyBeta);
        }
    }

}
