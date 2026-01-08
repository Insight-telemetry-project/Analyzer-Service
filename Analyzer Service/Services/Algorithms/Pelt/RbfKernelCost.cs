using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Constant;
using System.Threading.Tasks;

namespace Analyzer_Service.Services.Algorithms.Pelt
{
    public class RbfKernelCost : IRbfKernelCost
    {
        private double[,] kernelMatrix;
        private double[,] prefixMatrix;

        public int MinimumSize => ConstantPelt.MINIMUM_SEGMENT_LENGTH;

        public void Fit(List<double> signalValues)
        {
            int signalLength = signalValues.Count;

            kernelMatrix = new double[signalLength, signalLength];

            double[] squaredDistances = new double[signalLength * (signalLength - 1) / 2];

            Parallel.For(0, signalLength, leftIndex =>
            {
                double leftValue = signalValues[leftIndex];

                for (int rightIndex = leftIndex + 1; rightIndex < signalLength; rightIndex++)
                {
                    int flatIndex = ComputeFlatIndex(leftIndex, rightIndex, signalLength);
                    double difference = leftValue - signalValues[rightIndex];
                    squaredDistances[flatIndex] = difference * difference;
                }
            });

            Array.Sort(squaredDistances);

            double medianSquaredDistance = squaredDistances[squaredDistances.Length / 2];

            double sigmaSquared =
                medianSquaredDistance <= ConstantPelt.ZEROTO_LERANCE
                ? ConstantPelt.DEFAULT_SIGMA_VALUE
                : medianSquaredDistance / ConstantPelt.SIGMA_DIVISION_FACTOR;

            Parallel.For(0, signalLength, rowIndex =>
            {
                kernelMatrix[rowIndex, rowIndex] = ConstantPelt.DEFAULT_SIGMA_VALUE;

                double baseValue = signalValues[rowIndex];

                for (int colIndex = rowIndex + 1; colIndex < signalLength; colIndex++)
                {
                    double compareValue = signalValues[colIndex];
                    double difference = baseValue - compareValue;

                    double gaussianValue = Math.Exp(-(difference * difference) / (2.0 * sigmaSquared));

                    kernelMatrix[rowIndex, colIndex] = gaussianValue;
                    kernelMatrix[colIndex, rowIndex] = gaussianValue;
                }
            });

            prefixMatrix = new double[signalLength + 1, signalLength + 1];

            for (int row = 1; row <= signalLength; row++)
            {
                for (int col = 1; col <= signalLength; col++)
                {
                    prefixMatrix[row, col] =
                        kernelMatrix[row - 1, col - 1] +
                        prefixMatrix[row - 1, col] +
                        prefixMatrix[row, col - 1] -
                        prefixMatrix[row - 1, col - 1];
                }
            }
        }

        private int ComputeFlatIndex(int leftIndex, int rightIndex, int length)
        {
            return (leftIndex * length) - (leftIndex * (leftIndex + 1) / 2) + (rightIndex - leftIndex - 1);
        }

        public double ComputeError(int segmentStartIndex, int segmentEndIndex)
        {
            int segmentLength = segmentEndIndex - segmentStartIndex;

            double areaSum = prefixMatrix[segmentEndIndex, segmentEndIndex]
                - prefixMatrix[segmentStartIndex, segmentEndIndex]
                - prefixMatrix[segmentEndIndex, segmentStartIndex]
                + prefixMatrix[segmentStartIndex, segmentStartIndex];

            double diagonalValue = segmentLength;

            double errorValue = diagonalValue - areaSum / segmentLength;
            return errorValue;
        }
    }
}
