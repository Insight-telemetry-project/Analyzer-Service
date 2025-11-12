using Analyzer_Service.Models.Interface.Algorithms.Pelt;

namespace Analyzer_Service.Services.Algorithms.Pelt
{
    public class RbfKernelCost : IRbfKernelCost
    {
        private double[,] kernelMatrix;
        private double[,] prefixMatrix;
        private int minimumSize = 2;

        public int MinimumSize => minimumSize;

        public void Fit(IReadOnlyList<double> signal)
        {
            int length = signal.Count;
            kernelMatrix = new double[length, length];

            double[] distancesSquared = new double[length * (length - 1) / 2];
            int pos = 0;

            for (int i = 0; i < length - 1; i++)
            {
                double xi = signal[i];
                for (int j = i + 1; j < length; j++)
                {
                    double diff = xi - signal[j];
                    distancesSquared[pos] = diff * diff;
                    pos++;
                }
            }

            Array.Sort(distancesSquared);
            double median = distancesSquared[distancesSquared.Length / 2];
            double sigmaSquared = median <= 1e-12 ? 1.0 : median / 2.0;

            for (int i = 0; i < length; i++)
            {
                kernelMatrix[i, i] = 1.0;
                double xi = signal[i];

                for (int j = i + 1; j < length; j++)
                {
                    double diff = xi - signal[j];
                    double value = Math.Exp(-(diff * diff) / (2.0 * sigmaSquared));
                    kernelMatrix[i, j] = value;
                    kernelMatrix[j, i] = value;
                }
            }

            prefixMatrix = new double[length + 1, length + 1];

            for (int i = 1; i <= length; i++)
            {
                for (int j = 1; j <= length; j++)
                {
                    prefixMatrix[i, j] =
                        kernelMatrix[i - 1, j - 1] +
                        prefixMatrix[i - 1, j] +
                        prefixMatrix[i, j - 1] -
                        prefixMatrix[i - 1, j - 1];
                }
            }
        }

        public double ComputeError(int start, int end)
        {
            int length = end - start;
            if (length < minimumSize)
            {
                throw new InvalidOperationException("Segment is too short for RBF cost.");
            }

            double totalSum =
                prefixMatrix[end, end]
                - prefixMatrix[start, end]
                - prefixMatrix[end, start]
                + prefixMatrix[start, start];

            double diagonalSum = length;

            double value = diagonalSum - totalSum / length;
            return value;
        }


    }
}
