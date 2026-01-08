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
           
            ConstantPelt.SAMPLING_JUMP = 12;
            ConstantPelt.PENALTY_BETA = 1.0;
            ConstantPelt.MINIMUM_SEGMENT_DURATION_SECONDS = 1.5;

            ConstantAnomalyDetection.MINIMUM_DURATION_SECONDS = 0.7;
            ConstantAnomalyDetection.MINIMUM_RANGEZ = 0.9;
            ConstantAnomalyDetection.PATTERN_SUPPORT_THRESHOLD = 3;

            ConstantAnomalyDetection.FINAL_SCORE = 0.82;
            ConstantAnomalyDetection.HASH_SIMILARITY = 0.55;
            ConstantAnomalyDetection.FEATURE_SIMILARITY = 0.18;
            ConstantAnomalyDetection.DURATION_SIMILARITY = 0.05;

            ConstantAnomalyDetection.HASH_THRESHOLD = 0.01;

            ConstantAnomalyDetection.RARE_LABEL_COUNT_MAX = 7;
            ConstantAnomalyDetection.RARE_LABEL_TIME_FRACTION = 0.12;
            ConstantAnomalyDetection.POST_MINIMUM_GAP_SECONDS = 6;
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

            if (segmentLabel == ConstantRandomForest.RAMP_DOWN ||
                segmentLabel == ConstantRandomForest.SPIKE_LOW ||
                segmentLabel == ConstantRandomForest.BELOW_BOUND)
            {
                double minimumValue = segmentSignalSlice.Min();
                int localIndex = Array.IndexOf(segmentSignalSlice, minimumValue);
                return segmentStartIndex + localIndex;
            }
            
            if (segmentLabel == ConstantRandomForest.RAMP_UP ||
                segmentLabel == ConstantRandomForest.SPIKE_HIGH ||
                segmentLabel == ConstantRandomForest.ABOVE_BOUND)
            {
                double maximumValue = segmentSignalSlice.Max();
                int localIndex = Array.IndexOf(segmentSignalSlice, maximumValue);
                return segmentStartIndex + localIndex;
            }

            if (segmentLabel == ConstantRandomForest.OSCILLATION)
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
