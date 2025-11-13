using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;

namespace Analyzer_Service.Services.Algorithms.Pelt
{
    public class SignalPreprocessor : ISignalPreprocessor
    {
        public IReadOnlyList<double> Apply(
           IReadOnlyList<double> values,
           int hampelWindow,
           double hampelSigma
            )
        {
            double[] workingSignal = values.ToArray();

            workingSignal = ApplyHampelFilter(workingSignal, hampelWindow, hampelSigma);
            workingSignal = ApplyZScoreNormalization(workingSignal);

            return workingSignal;
        }

        private double[] ApplyZScoreNormalization(double[] values)
        {
            double mean = values.AsParallel().Average();
            double varianceSum = values
                       .AsParallel()
                       .Select(value => (value - mean) * (value - mean))
                        .Sum();

            double standardDeviation = Math.Sqrt(varianceSum / values.Length);
            if (standardDeviation <= ConstantAlgorithm.Epsilon)
                standardDeviation = 1.0;

            double[] normalized = new double[values.Length];
            Parallel.For(0, values.Length, index =>
            {
                normalized[index] = (values[index] - mean) / standardDeviation;
            });
            return normalized;
        }

        private double[] ApplyHampelFilter(double[] signal, int windowSize, double sigmaThreshold)
        {
            int length = signal.Length;
            if (length == 0) return signal;

            if (windowSize < 1) windowSize = 1;
            if (windowSize % 2 == 0) windowSize += 1;
            int halfWindow = windowSize / 2;

            double[] medianValues = new double[length];

            Parallel.For(0, length, index =>
            {
                int startIndex = Math.Max(index - halfWindow, 0);
                int endIndex = Math.Min(index + halfWindow, length - 1);

                double[] windowArray = signal[startIndex..(endIndex + 1)];
                Array.Sort(windowArray);

                medianValues[index] = windowArray[windowArray.Length / 2];
            });

            double[] deviationFromMedian = new double[length];

            Parallel.For(0, length, index =>
            {
                deviationFromMedian[index] = Math.Abs(signal[index] - medianValues[index]);
            });


            double[] madValues = new double[length];

            Parallel.For(0, length, index =>
            {
                int startIndex = Math.Max(index - halfWindow, 0);
                int endIndex = Math.Min(index + halfWindow, length - 1);

                double[] windowArray = deviationFromMedian[startIndex..(endIndex + 1)];
                Array.Sort(windowArray);

                madValues[index] = windowArray[windowArray.Length / 2];
            });

            double[] threshold = new double[length];

            Parallel.For(0, length, index =>
            {
                threshold[index] =
                    sigmaThreshold *
                    ConstantAlgorithm.MadToStdScale *
                    (madValues[index] + ConstantAlgorithm.Epsilon);
            });

            double[] filtered = (double[])signal.Clone();

            Parallel.For(0, length, index =>
            {
                if (Math.Abs(signal[index] - medianValues[index]) > threshold[index])
                {
                    filtered[index] = medianValues[index];
                }
            });

            return filtered;

        }
    }
}