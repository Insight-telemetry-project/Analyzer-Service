using Analyzer_Service.Models.Dto;
using Analyzer_Service.Services.Algorithms.Pelt;

namespace Analyzer_Service.Models.Interface.Algorithms.AnomalyDetector
{
    public interface ISignalNoiseTuning
    {
        void ApplyHighNoiseConfiguration();

        void ApplyLowNoiseConfiguration();

        int SelectRepresentativeSampleIndex(
            List<double> processedSignalValues,
            SegmentBoundary segmentBoundary,
            string segmentLabel);

        double ComputeAnomalyStrengthScore(
            SegmentFeatures segmentFeatures);


        void Apply(PeltTuningSettings settings);
    }
}
