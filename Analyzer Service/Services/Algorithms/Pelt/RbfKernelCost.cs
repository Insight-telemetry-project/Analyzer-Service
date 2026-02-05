using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using System.Buffers;

namespace Analyzer_Service.Services.Algorithms.Pelt
{
    public class RbfKernelCost : IRbfKernelCost
    {
        private double[,] kernelMatrix = new double[0, 0];
        private double[,] prefixMatrix = new double[0, 0];

        public int MinimumSize { get; } = ConstantPelt.MINIMUM_SEGMENT_LENGTH;

        public void Fit(double[] signalValues)
        {
            int signalLength = signalValues.Length;

            if (kernelMatrix.GetLength(0) != signalLength)
            {
                kernelMatrix = new double[signalLength, signalLength];
                prefixMatrix = new double[signalLength + 1, signalLength + 1];
            }
            else
            {
                Array.Clear(kernelMatrix, 0, kernelMatrix.Length);
                Array.Clear(prefixMatrix, 0, prefixMatrix.Length);
            }

            int pairCount = signalLength * (signalLength - 1) / 2;
            double[] squaredDistancesBuffer = ArrayPool<double>.Shared.Rent(Math.Max(pairCount, 1));

            try
            {
                Parallel.For(0, signalLength, leftIndex =>
                {
                    double leftValue = signalValues[leftIndex];

                    for (int rightIndex = leftIndex + 1; rightIndex < signalLength; rightIndex++)
                    {
                        int flatIndex = ComputeFlatIndex(leftIndex, rightIndex, signalLength);
                        double difference = leftValue - signalValues[rightIndex];
                        squaredDistancesBuffer[flatIndex] = difference * difference;
                    }
                });

                Array.Sort(squaredDistancesBuffer, 0, pairCount);

                double medianSquaredDistance = pairCount == 0 ? 0.0 : squaredDistancesBuffer[pairCount / 2];

                double sigmaSquared =
                    medianSquaredDistance <= ConstantPelt.ZEROTO_LERANCE
                        ? ConstantPelt.DEFAULT_SIGMA_VALUE
                        : medianSquaredDistance / ConstantPelt.SIGMA_DIVISION_FACTOR;

                Parallel.For(0, signalLength, rowIndex =>
                {
                    kernelMatrix[rowIndex, rowIndex] = 1.0;

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
            finally
            {
                ArrayPool<double>.Shared.Return(squaredDistancesBuffer, true);
            }
        }

        private int ComputeFlatIndex(int leftIndex, int rightIndex, int length)
        {
            return (leftIndex * length) - (leftIndex * (leftIndex + 1) / 2) + (rightIndex - leftIndex - 1);
        }

        public double ComputeError(int segmentStartIndex, int segmentEndIndex)
        {
            int segmentLength = segmentEndIndex - segmentStartIndex;

            double areaSum =
                prefixMatrix[segmentEndIndex, segmentEndIndex]
                - prefixMatrix[segmentStartIndex, segmentEndIndex]
                - prefixMatrix[segmentEndIndex, segmentStartIndex]
                + prefixMatrix[segmentStartIndex, segmentStartIndex];

            double diagonalValue = segmentLength;
            double errorValue = diagonalValue - areaSum / segmentLength;

            return errorValue;
        }
    }
}
