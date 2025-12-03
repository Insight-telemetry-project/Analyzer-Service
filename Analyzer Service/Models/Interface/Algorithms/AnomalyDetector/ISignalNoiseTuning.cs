using Analyzer_Service.Models.Dto;

namespace Analyzer_Service.Models.Interface.Algorithms.AnomalyDetector
{
    public interface ISignalNoiseTuning
    {
        void ApplyConstantPeltConfiguration();

        int SelectRepresentativeSampleIndex(
            List<double> processedSignalValues,
            SegmentBoundary segmentBoundary,
            string segmentLabel);

        double ComputeAnomalyStrengthScore(
            Dictionary<string, double> featureDictionary);
    }
}
