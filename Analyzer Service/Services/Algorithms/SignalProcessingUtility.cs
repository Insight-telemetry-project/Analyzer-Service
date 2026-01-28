using System;
using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Interface.Algorithms;

namespace Analyzer_Service.Services.Algorithms
{
    public class SignalProcessingUtility : ISignalProcessingUtility
    {
        public double[] ApplyHampel(IReadOnlyList<double> inputValues,
            int windowSize,double sigma)
        {
            int totalSampleCount = inputValues.Count;

            if (windowSize % 2 == 0)
            {
                windowSize = windowSize + 1;
            }

            int halfWindowSize = windowSize / 2;

            double[] filteredValues = new double[totalSampleCount];

            for (int sampleIndex = 0; sampleIndex < totalSampleCount; sampleIndex++)
            {
                filteredValues[sampleIndex] = inputValues[sampleIndex];
            }

            double[] windowBuffer = new double[windowSize];
            double[] deviationBuffer = new double[windowSize];

            for (int centerSampleIndex = 0; centerSampleIndex < totalSampleCount; centerSampleIndex++)
            {
                int windowStartIndex = Math.Max(0, centerSampleIndex - halfWindowSize);
                int windowEndIndex = Math.Min(totalSampleCount - 1, centerSampleIndex + halfWindowSize);
                int currentWindowLength = windowEndIndex - windowStartIndex + 1;

                for (int windowOffsetIndex = 0; windowOffsetIndex < currentWindowLength; windowOffsetIndex++)
                {
                    windowBuffer[windowOffsetIndex] =
                        inputValues[windowStartIndex + windowOffsetIndex];
                }

                double windowMedian =
                    ComputeMedian(windowBuffer, currentWindowLength);

                for (int windowOffsetIndex = 0; windowOffsetIndex < currentWindowLength; windowOffsetIndex++)
                {
                    deviationBuffer[windowOffsetIndex] =
                        Math.Abs(windowBuffer[windowOffsetIndex] - windowMedian);
                }

                double medianAbsoluteDeviation =
                    ComputeMedian(deviationBuffer, currentWindowLength);

                double deviationThreshold =
                    sigma
                    * ConstantAlgorithm.THRESHOLD_FORMULA
                    * (medianAbsoluteDeviation + ConstantAlgorithm.EPSILON);

                double centerSampleValue = inputValues[centerSampleIndex];

                if (Math.Abs(centerSampleValue - windowMedian) > deviationThreshold)
                {
                    filteredValues[centerSampleIndex] = windowMedian;
                }
            }

            return filteredValues;
        }



        public double[] ApplyZScore(IReadOnlyList<double> values)
        {
            int count = values.Count;

            double sum = 0.0;
            for (int index = 0; index < count; index++)
            {
                sum += values[index];
            }

            double mean = sum / count;

            double varianceSum = 0.0;
            for (int index = 0; index < count; index++)
            {
                double delta = values[index] - mean;
                varianceSum += delta * delta;
            }

            double std = Math.Sqrt(varianceSum / count);
            if (std < ConstantAlgorithm.EPSILON)
            {
                std = 1.0;
            }

            double[] output = new double[count];
            for (int index = 0; index < count; index++)
            {
                output[index] = (values[index] - mean) / std;
            }

            return output;
        }


        public double ComputeMedian(double[] values)
        {
            double[] sorted = (double[])values.Clone();
            return ComputeMedian(sorted, sorted.Length);
        }

        public double ComputeMedian(double[] buffer, int length)
        {
            Array.Sort(buffer, 0, length);

            int midIndex = length / 2;

            if (length % 2 == 1)
            {
                return buffer[midIndex];
            }

            return 0.5 * (buffer[midIndex - 1] + buffer[midIndex]);
        }


    }
}
