using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Interface.Algorithms;

namespace Analyzer_Service.Services.Algorithms
{
    public class SignalProcessingUtility : ISignalProcessingUtility
    {
        public double[] ApplyHampel(double[] inputValues, int windowSize, double sigma) // Discussion: this method is too long, break it into smaller methods and / or use oop principles
        { // Discussion: Also the nesting level is too deep in some places, you can also fix this is a couple of ways, like spliting into methods, using linq, etc.
            int valueCount = inputValues.Length;

            if (windowSize % 2 == 0) // Discussion: avoid magic numbers, you can fix this in a couple of ways, constants, split into methods with meaningful names, enums, etc.
            {
                windowSize = windowSize + 1;
            }

            int halfWindow = windowSize / 2;

            double[] outputValues = new double[valueCount];
            Array.Copy(inputValues, outputValues, valueCount); // Discussion: you are allocationg memory unnecessarily here, you can just modify the input array directly or create a new one without copying the data

            for (int index = 0; index < valueCount; index++) // Discussion: generic names like index, value, item, etc. should be avoided, use meaningful names that explain what the variable represents
            {
                int windowStart = Math.Max(0, index - halfWindow);
                int windowEnd = Math.Min(valueCount - 1, index + halfWindow);
                int windowLength = windowEnd - windowStart + 1;

                double[] localWindow = new double[windowLength];
                for (int indexWindow = 0; indexWindow < windowLength; indexWindow++) // Discussion: you can use linq here to avoid unnecessary nesting
                {
                    localWindow[indexWindow] = inputValues[windowStart + indexWindow];
                }

                double median = ComputeMedian(localWindow);

                double[] absoluteDeviation = new double[windowLength];
                for (int indexWindow = 0; indexWindow < windowLength; indexWindow++) // Discussion: you can use linq here to avoid unnecessary nesting
                {
                    absoluteDeviation[indexWindow] = Math.Abs(localWindow[indexWindow] - median);
                }

                double mad = ComputeMedian(absoluteDeviation);
                double threshold = sigma * ConstantAlgorithm.THRESHOLD_FORMULA * (mad + ConstantAlgorithm.Epsilon);

                if (Math.Abs(inputValues[index] - median) > threshold) // Discussion: the goal is to make the code understandable to every programmer even if they don't know the algorithm, so avoid label complex calculations with meaningful names (using oop, methods, variables, etc.)
                {
                    outputValues[index] = median;
                }
            }

            return outputValues;
        }

        public List<double> ApplyZScore(double[] values)
        {
            int count = values.Length;

            double sum = 0.0;
            for (int index = 0; index < count; index++) // Discussion: can you linq here
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
            if (std < ConstantAlgorithm.Epsilon) // Discussion: can you shorthand if here to change lines 73 to 77 to one line
            {
                std = 1.0;
            }

            double[] output = new double[count];
            for (int index = 0; index < count; index++) // Discussion: can use linq here
            {
                output[index] = (values[index] - mean) / std;
            }

            return output.ToList(); // Discussion: you are allocationg memory unnecessarily here, you can change the return type to IEnumerable<double> (along with using the mongo cursor properly to manipulate the ram in place instead of fetching it all at once like we talked about)
        }


        public double ComputeMedian(double[] values)
        {
            double[] sorted = (double[])values.Clone();
            Array.Sort(sorted);

            int count = sorted.Length;
            int midIndex = count / 2;

            if (count % 2 == 1) // Discussion: label your calculations with meaningful names to improve code readability, for example "isCountOdd"
            {
                return sorted[midIndex];
            }

            return 0.5 * (sorted[midIndex - 1] + sorted[midIndex]); // Discussion: same as above, label the calculation with a meaningful names
        }

    }
}
