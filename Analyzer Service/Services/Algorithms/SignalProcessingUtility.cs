using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Interface.Algorithms;
using System;
using System.Buffers;

namespace Analyzer_Service.Services.Algorithms
{
    public class SignalProcessingUtility : ISignalProcessingUtility
    {
        public double[] ApplyHampel(double[] inputValues, int windowSize, double sigma)
        {
            int totalValueCount = inputValues.Length;

            if (windowSize % 2 == 0)
            {
                windowSize = windowSize + 1;
            }

            int halfWindowSize = windowSize / 2;

            double[] filteredOutputValues = new double[totalValueCount];
            Array.Copy(inputValues, filteredOutputValues, totalValueCount);

            double[] windowBuffer = ArrayPool<double>.Shared.Rent(windowSize);
            double[] deviationBuffer = ArrayPool<double>.Shared.Rent(windowSize);

            try
            {
                for (int centerIndex = 0; centerIndex < totalValueCount; centerIndex++)
                {
                    int windowStartIndex = Math.Max(0, centerIndex - halfWindowSize);
                    int windowEndIndex = Math.Min(totalValueCount - 1, centerIndex + halfWindowSize);

                    int currentWindowLength = windowEndIndex - windowStartIndex + 1;

                    for (int windowOffset = 0; windowOffset < currentWindowLength; windowOffset++)
                    {
                        windowBuffer[windowOffset] =
                            inputValues[windowStartIndex + windowOffset];
                    }

                    double medianValue =
                        ComputeMedianFromPrefix(windowBuffer, currentWindowLength);

                    for (int windowOffset = 0; windowOffset < currentWindowLength; windowOffset++)
                    {
                        deviationBuffer[windowOffset] =
                            Math.Abs(windowBuffer[windowOffset] - medianValue);
                    }

                    double medianAbsoluteDeviation =
                        ComputeMedianFromPrefix(deviationBuffer, currentWindowLength);

                    double threshold =
                        sigma *
                        ConstantAlgorithm.THRESHOLD_FORMULA *
                        (medianAbsoluteDeviation + ConstantAlgorithm.EPSILON);

                    if (Math.Abs(inputValues[centerIndex] - medianValue) > threshold)
                    {
                        filteredOutputValues[centerIndex] = medianValue;
                    }
                }
            }
            finally
            {
                ArrayPool<double>.Shared.Return(windowBuffer);
                ArrayPool<double>.Shared.Return(deviationBuffer);
            }

            return filteredOutputValues;
        }

        private double ComputeMedianFromPrefix(double[] buffer, int length)
        {
            double[] sortedCopy = new double[length];
            Array.Copy(buffer, sortedCopy, length);

            Array.Sort(sortedCopy);

            int middleIndex = length / 2;

            if (length % 2 == 1)
            {
                return sortedCopy[middleIndex];
            }

            return 0.5 * (sortedCopy[middleIndex - 1] + sortedCopy[middleIndex]);
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
