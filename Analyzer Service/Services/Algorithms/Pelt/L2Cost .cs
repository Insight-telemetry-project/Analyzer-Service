using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;

namespace Analyzer_Service.Services.Algorithms.Pelt
{
    public class L2Cost : IRbfKernelCost
    {
        private double[] prefixSumValues = Array.Empty<double>();
        private double[] prefixSumSquaredValues = Array.Empty<double>();

        public int MinimumSize { get; } = ConstantPelt.MINIMUM_SEGMENT_LENGTH;

        public void Fit(double[] signalValues)
        {
            int sampleCount = signalValues.Length;
            int requiredPrefixLength = sampleCount + 1;

            if (prefixSumValues.Length != requiredPrefixLength)
            {
                prefixSumValues = new double[requiredPrefixLength];
                prefixSumSquaredValues = new double[requiredPrefixLength];
            }
            else
            {
                Array.Clear(prefixSumValues, 0, prefixSumValues.Length);
                Array.Clear(prefixSumSquaredValues, 0, prefixSumSquaredValues.Length);
            }

            for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                double currentValue = signalValues[sampleIndex];
                int prefixIndex = sampleIndex + 1;

                prefixSumValues[prefixIndex] = prefixSumValues[sampleIndex] + currentValue;
                prefixSumSquaredValues[prefixIndex] = prefixSumSquaredValues[sampleIndex] + (currentValue * currentValue);
            }
        }

        public double ComputeError(int segmentStartIndex, int segmentEndIndex)
        {
            int segmentLength = segmentEndIndex - segmentStartIndex;

            double segmentValueSum =
                prefixSumValues[segmentEndIndex] - prefixSumValues[segmentStartIndex];

            double segmentSquaredValueSum =
                prefixSumSquaredValues[segmentEndIndex] - prefixSumSquaredValues[segmentStartIndex];

            double meanSquareTerm = (segmentValueSum * segmentValueSum) / segmentLength;
            double sumSquaredError = segmentSquaredValueSum - meanSquareTerm;

            if (sumSquaredError < 0.0 && sumSquaredError > -1e-12)
            {
                sumSquaredError = 0.0;
            }

            return sumSquaredError;
        }
    }
}
