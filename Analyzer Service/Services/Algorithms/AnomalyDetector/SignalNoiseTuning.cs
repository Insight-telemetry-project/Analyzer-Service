using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms.AnomalyDetector;

namespace Analyzer_Service.Services.Algorithms.AnomalyDetector
{
    public class SignalNoiseTuning : ISignalNoiseTuning
    {
        public void ApplyConstantPeltConfiguration()
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

        public int SelectRepresentativeSampleIndex(
            List<double> processedSignalValues,
            SegmentBoundary segmentBoundary,
            string segmentLabel)
        {
            int segmentStartIndex = segmentBoundary.StartIndex;
            int segmentEndIndex = segmentBoundary.EndIndex;

            double[] segmentSignalSlice = processedSignalValues
                .Skip(segmentStartIndex)
                .Take(segmentEndIndex - segmentStartIndex + 1)
                .ToArray();

            if (segmentLabel == "RampDown" ||
                segmentLabel == "SpikeLow" ||
                segmentLabel == "BelowBound")
            {
                double minimumValue = segmentSignalSlice.Min();
                int localMinimumIndex =
                    Array.IndexOf(segmentSignalSlice, minimumValue);

                return segmentStartIndex + localMinimumIndex;
            }

            if (segmentLabel == "RampUp" ||
                segmentLabel == "SpikeHigh" ||
                segmentLabel == "AboveBound")
            {
                double maximumValue = segmentSignalSlice.Max();
                int localMaximumIndex =
                    Array.IndexOf(segmentSignalSlice, maximumValue);

                return segmentStartIndex + localMaximumIndex;
            }

            if (segmentLabel == "Oscillation")
            {
                double maximumAbsoluteValue =
                    segmentSignalSlice.Max(value => Math.Abs(value));

                double valueWithMaximumAbsolute =
                    segmentSignalSlice.First(value => Math.Abs(value) == maximumAbsoluteValue);

                int localOscillationIndex =
                    Array.IndexOf(segmentSignalSlice, valueWithMaximumAbsolute);

                return segmentStartIndex + localOscillationIndex;
            }

            double globalMaximumAbsoluteValue =
                segmentSignalSlice.Max(value => Math.Abs(value));

            int localIndexOfGlobalAbsMax =
                Array.IndexOf(segmentSignalSlice, globalMaximumAbsoluteValue);

            return segmentStartIndex + localIndexOfGlobalAbsMax;
        }

        public double ComputeAnomalyStrengthScore(
            Dictionary<string, double> featureDictionary)
        {
            double rangeZScore = Math.Abs(featureDictionary[ConstantRandomForest.RANGE_Z_JSON]);
            double energyZScore = featureDictionary[ConstantRandomForest.ENERGY_Z_JSON];
            double weightedStrengthScore = rangeZScore * ConstantAnomalyDetection.RANGEZ_SCORE_THRESHOLD + energyZScore * ConstantAnomalyDetection.ENERGYZ_SCORE_THRESHOLD;

            return weightedStrengthScore;
        }
    }
}
