using Analyzer_Service.Models.Interface.Algorithms.Pelt;

namespace Analyzer_Service.Services.Algorithms.Pelt
{
    public class SignalPreprocessor : ISignalPreprocessor
    {
        public IReadOnlyList<double> Apply(
           IReadOnlyList<double> values,
           bool useHampel,
           int hampelWindow,
           double hampelSigma,
           bool useZScore)
        {
            double[] workingSignal = values.ToArray();

            if (useHampel)
                workingSignal = ApplyHampelFilter(workingSignal, hampelWindow, hampelSigma);

            if (useZScore)
                workingSignal = ApplyZScoreNormalization(workingSignal);

            return workingSignal;
        }

        private double[] ApplyZScoreNormalization(double[] values)
        {
            double mean = values.Average();
            double varianceSum = 0.0;

            for (int index = 0; index < values.Length; index++)
            {
                double centeredValue = values[index] - mean;
                varianceSum += centeredValue * centeredValue;
            }

            double standardDeviation = Math.Sqrt(varianceSum / values.Length);
            if (standardDeviation <= 1e-12) standardDeviation = 1.0;

            double[] normalized = new double[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                normalized[index] = (values[index] - mean) / standardDeviation;
            }

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
            for (int index = 0; index < length; index++)
            {
                int startIndex = Math.Max(index - halfWindow, 0);
                int endIndex = Math.Min(index + halfWindow, length - 1);
                double[] windowArray = signal[startIndex..(endIndex + 1)];
                Array.Sort(windowArray);
                medianValues[index] = windowArray[windowArray.Length / 2];
            }

            double[] deviationFromMedian = new double[length];
            for (int index = 0; index < length; index++)
            {
                deviationFromMedian[index] = Math.Abs(signal[index] - medianValues[index]);
            }

            double[] madValues = new double[length];
            for (int index = 0; index < length; index++)
            {
                int startIndex = Math.Max(index - halfWindow, 0);
                int endIndex = Math.Min(index + halfWindow, length - 1);
                double[] windowArray = deviationFromMedian[startIndex..(endIndex + 1)];
                Array.Sort(windowArray);
                madValues[index] = windowArray[windowArray.Length / 2];
            }

            double[] threshold = new double[length];
            for (int index = 0; index < length; index++)
            {
                threshold[index] = sigmaThreshold * 1.4826 * (madValues[index] + 1e-12);
            }

            double[] filtered = (double[])signal.Clone();
            for (int index = 0; index < length; index++)
            {
                if (Math.Abs(signal[index] - medianValues[index]) > threshold[index])
                {
                    filtered[index] = medianValues[index];
                }
            }

            return filtered;
        }
    }
}
