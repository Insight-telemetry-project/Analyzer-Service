using Analyzer_Service.Models.Dto;

namespace Analyzer_Service.Models.Interface.Algorithms.AnomalyDetector
{
    public interface ISignalNoiseTuning
    {
        public void ApplyConstantPeltConfiguration();

        public int SelectRepresentativeSampleIndex(
            List<double> processedSignalValues,
            SegmentBoundary segmentBoundary,
            string segmentLabel);

        public double ComputeAnomalyStrengthScore(
            Dictionary<string, double> featureDictionary);
    }
}
