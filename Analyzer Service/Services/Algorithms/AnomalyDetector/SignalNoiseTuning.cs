using System;
using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms.AnomalyDetector;

namespace Analyzer_Service.Services.Algorithms.AnomalyDetector
{
    public class SignalNoiseTuning : ISignalNoiseTuning
    {
        public void ApplyLowNoiseConfiguration()
        {
            // your commented config stays as-is
        }

        public void ApplyHighNoiseConfiguration()
        {
            // your commented config stays as-is
        }

        public int SelectRepresentativeSampleIndex(
            double[] processedSignalValues,
            SegmentBoundary segmentBoundary,
            string segmentLabel)
        {
            int segmentStartIndex = segmentBoundary.StartIndex;
            int segmentEndIndex = segmentBoundary.EndIndex;

            if (processedSignalValues == null || processedSignalValues.Length == 0)
            {
                return segmentStartIndex;
            }

            if (segmentStartIndex < 0)
            {
                segmentStartIndex = 0;
            }

            if (segmentEndIndex >= processedSignalValues.Length)
            {
                segmentEndIndex = processedSignalValues.Length - 1;
            }

            if (segmentEndIndex < segmentStartIndex)
            {
                return segmentStartIndex;
            }

            bool chooseMinimum =
                segmentLabel == ConstantRandomForest.RAMP_DOWN ||
                segmentLabel == ConstantRandomForest.SPIKE_LOW ||
                segmentLabel == ConstantRandomForest.BELOW_BOUND;

            bool chooseMaximum =
                segmentLabel == ConstantRandomForest.RAMP_UP ||
                segmentLabel == ConstantRandomForest.SPIKE_HIGH ||
                segmentLabel == ConstantRandomForest.ABOVE_BOUND;

            bool chooseMaxAbs =
                segmentLabel == ConstantRandomForest.OSCILLATION;

            int bestIndex = segmentStartIndex;

            if (chooseMinimum)
            {
                double bestValue = processedSignalValues[segmentStartIndex];

                for (int index = segmentStartIndex + 1; index <= segmentEndIndex; index++)
                {
                    double currentValue = processedSignalValues[index];
                    if (currentValue < bestValue)
                    {
                        bestValue = currentValue;
                        bestIndex = index;
                    }
                }

                return bestIndex;
            }

            if (chooseMaximum)
            {
                double bestValue = processedSignalValues[segmentStartIndex];

                for (int index = segmentStartIndex + 1; index <= segmentEndIndex; index++)
                {
                    double currentValue = processedSignalValues[index];
                    if (currentValue > bestValue)
                    {
                        bestValue = currentValue;
                        bestIndex = index;
                    }
                }

                return bestIndex;
            }

            // OSCILLATION and default: choose maximum absolute value
            double bestAbsValue = Math.Abs(processedSignalValues[segmentStartIndex]);

            for (int index = segmentStartIndex + 1; index <= segmentEndIndex; index++)
            {
                double currentAbsValue = Math.Abs(processedSignalValues[index]);
                if (currentAbsValue > bestAbsValue)
                {
                    bestAbsValue = currentAbsValue;
                    bestIndex = index;
                }
            }

            return bestIndex;
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
