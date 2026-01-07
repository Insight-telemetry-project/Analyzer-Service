using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms.AnomalyDetector;

namespace Analyzer_Service.Services.Algorithms.AnomalyDetector
{
    public class SignalNoiseTuning : ISignalNoiseTuning
    {
        public void ApplyHighNoiseConfiguration()
        {
            ConstantPelt.SAMPLING_JUMP = 10;
            ConstantPelt.PENALTY_BETA = 0.5;
            ConstantPelt.MINIMUM_SEGMENT_DURATION_SECONDS = 1.2;

            ConstantAnomalyDetection.MINIMUM_DURATION_SECONDS = 0.5;
            ConstantAnomalyDetection.MINIMUM_RANGEZ = 1.2;
            ConstantAnomalyDetection.PATTERN_SUPPORT_THRESHOLD = 4;

            ConstantAnomalyDetection.FINAL_SCORE = 0.9;
            ConstantAnomalyDetection.HASH_SIMILARITY = 0.55;
            ConstantAnomalyDetection.FEATURE_SIMILARITY = 0.2;
            ConstantAnomalyDetection.DURATION_SIMILARITY = 0.05;

            ConstantAnomalyDetection.HASH_THRESHOLD = 0.015;
            ConstantAnomalyDetection.RARE_LABEL_COUNT_MAX = 4;
            ConstantAnomalyDetection.RARE_LABEL_TIME_FRACTION = 0.1;
            ConstantAnomalyDetection.POST_MINIMUM_GAP_SECONDS = 10;
        }

        public void ApplyLowNoiseConfiguration()
        {
            // Faster configuration for noisy / jumpy flights:
            // Goal: fewer segments + less downstream work, while still catching strong abrupt jumps.

            // --- PELT / segmentation knobs (biggest impact on speed) ---
            ConstantPelt.SAMPLING_JUMP = 12;                 // was 3 -> much fewer evaluated points
            ConstantPelt.PENALTY_BETA = 1.0;                 // was 0.25 -> stronger penalty => fewer cuts
            ConstantPelt.MINIMUM_SEGMENT_DURATION_SECONDS = 1.5; // was 0.6 -> prevents micro-segments

            // --- Anomaly detection knobs (reduce candidate anomalies / comparisons) ---
            ConstantAnomalyDetection.MINIMUM_DURATION_SECONDS = 0.7;   // was 0.3 -> ignore ultra-short noise
            ConstantAnomalyDetection.MINIMUM_RANGEZ = 0.9;             // was 0.5 -> ignore small z-range wiggles
            ConstantAnomalyDetection.PATTERN_SUPPORT_THRESHOLD = 3;    // was 2 -> require more support

            // Keep scoring reasonably strict, but not too harsh (so big spikes still pass)
            ConstantAnomalyDetection.FINAL_SCORE = 0.82;               // was 0.75
            ConstantAnomalyDetection.HASH_SIMILARITY = 0.55;           // was 0.45
            ConstantAnomalyDetection.FEATURE_SIMILARITY = 0.18;        // was 0.15
            ConstantAnomalyDetection.DURATION_SIMILARITY = 0.05;       // keep as-is

            // Hash threshold: raise a bit to reduce expensive/low-value matches
            ConstantAnomalyDetection.HASH_THRESHOLD = 0.01;            // was 0.02 in your tuning; choose stable middle

            // Rare label heuristics: allow a bit more segments before marking rare (noisy flights create many segments)
            ConstantAnomalyDetection.RARE_LABEL_COUNT_MAX = 7;         // was 8
            ConstantAnomalyDetection.RARE_LABEL_TIME_FRACTION = 0.12;  // was 0.15
            ConstantAnomalyDetection.POST_MINIMUM_GAP_SECONDS = 6;     // was 3 -> prevents dense anomaly spam
        }


        public int SelectRepresentativeSampleIndex(
            List<double> processedSignalValues,
            SegmentBoundary segmentBoundary,
            string segmentLabel)
        {
            int segmentStartIndex = segmentBoundary.StartIndex;
            int segmentEndIndex = segmentBoundary.EndIndex;

            double[] segmentSignalSlice =
                processedSignalValues
                    .Skip(segmentStartIndex)
                    .Take(segmentEndIndex - segmentStartIndex + 1)
                    .ToArray();

            if (segmentLabel == "RampDown" ||
                segmentLabel == "SpikeLow" ||
                segmentLabel == "BelowBound")
            {
                double minimumValue = segmentSignalSlice.Min();
                int localIndex = Array.IndexOf(segmentSignalSlice, minimumValue);
                return segmentStartIndex + localIndex;
            }

            if (segmentLabel == "RampUp" ||
                segmentLabel == "SpikeHigh" ||
                segmentLabel == "AboveBound")
            {
                double maximumValue = segmentSignalSlice.Max();
                int localIndex = Array.IndexOf(segmentSignalSlice, maximumValue);
                return segmentStartIndex + localIndex;
            }

            if (segmentLabel == "Oscillation")
            {
                double maxAbs = segmentSignalSlice.Max(value => Math.Abs(value));
                double valueWithMaxAbs =
                    segmentSignalSlice.First(value => Math.Abs(value) == maxAbs);

                int localIndex = Array.IndexOf(segmentSignalSlice, valueWithMaxAbs);
                return segmentStartIndex + localIndex;
            }

            double globalMaxAbs = segmentSignalSlice.Max(value => Math.Abs(value));
            double globalValueWithMaxAbs =
                segmentSignalSlice.First(value => Math.Abs(value) == globalMaxAbs);

            int globalIndex =
                Array.IndexOf(segmentSignalSlice, globalValueWithMaxAbs);

            return segmentStartIndex + globalIndex;
        }

        public double ComputeAnomalyStrengthScore(SegmentFeatures segmentFeatures)
        {
            double rangeZ = Math.Abs(segmentFeatures.RangeZ);
            double energyZ = segmentFeatures.EnergyZ;

            return
                rangeZ * ConstantAnomalyDetection.RANGEZ_SCORE_THRESHOLD +
                energyZ * ConstantAnomalyDetection.ENERGYZ_SCORE_THRESHOLD;
        }
    }
}
