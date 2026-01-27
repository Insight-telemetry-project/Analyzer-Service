using Analyzer_Service.Models.Dto;
using Analyzer_Service.Services.Algorithms.Pelt;

namespace Analyzer_Service.Models.Interface.Algorithms.AnomalyDetector
{
    public interface ISignalNoiseTuning
    {
        void ApplyHighNoiseConfiguration();

        int SelectRepresentativeSampleIndex(
            double[] processedSignalValues,
            SegmentBoundary segmentBoundary,
            string segmentLabel);

        double ComputeAnomalyStrengthScore(SegmentFeatures segmentFeatures);
    }
}
