using System;
using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Interface.Algorithms;

namespace Analyzer_Service.Services.Algorithms
{
    public class SignalProcessingUtility : ISignalProcessingUtility
    {
        public double[] ApplyHampel(double[] inputValues, int windowSize, double sigma)
        {
            int valueCount = inputValues.Length;

            if (windowSize % 2 == 0)
            {
                windowSize = windowSize + 1;
            }

            int halfWindow = windowSize / 2;

            double[] outputValues = new double[valueCount];
            Array.Copy(inputValues, outputValues, valueCount);

            for (int index = 0; index < valueCount; index++)
            {
                int windowStart = Math.Max(0, index - halfWindow);
                int windowEnd = Math.Min(valueCount - 1, index + halfWindow);
                int windowLength = windowEnd - windowStart + 1;

                double[] localWindow = new double[windowLength];
                for (int indexWindow = 0; indexWindow < windowLength; indexWindow++)
                {
                    localWindow[indexWindow] = inputValues[windowStart + indexWindow];
                }

                double median = ComputeMedian(localWindow);

                double[] absoluteDeviation = new double[windowLength];
                for (int indexWindow = 0; indexWindow < windowLength; indexWindow++)
                {
                    absoluteDeviation[indexWindow] = Math.Abs(localWindow[indexWindow] - median);
                }

                double mad = ComputeMedian(absoluteDeviation);
                double threshold = sigma * ConstantAlgorithm.THRESHOLD_FORMULA * (mad + ConstantAlgorithm.EPSILON);

                if (Math.Abs(inputValues[index] - median) > threshold)
                {
                    outputValues[index] = median;
                }
            }

            return outputValues;
        }

        public double[] ApplyZScore(double[] values)
        {
            int count = values.Length;

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
            Array.Sort(sorted);

            int count = sorted.Length;
            int midIndex = count / 2;

            if (count % 2 == 1)
            {
                return sorted[midIndex];
            }

            return 0.5 * (sorted[midIndex - 1] + sorted[midIndex]);
        }
    }
}
