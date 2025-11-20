using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;

namespace Analyzer_Service.Services.Algorithms.Pelt
{
    public class SignalPreprocessor : ISignalPreprocessor
    {
        public double[] Apply(
            List<double> inputSignalValues,
            int hampelWindowSize,
            double hampelSigmaThreshold)
        {
            double[] processedSignal = inputSignalValues.ToArray();

            processedSignal = ApplyHampelFilter(processedSignal, hampelWindowSize, hampelSigmaThreshold);
            processedSignal = ApplyZScoreNormalization(processedSignal);

            return processedSignal;
        }


        private double[] ApplyZScoreNormalization(double[] signalValues)
        {
            double meanValue = signalValues.AsParallel().Average();

            double varianceSum = signalValues.AsParallel()
                            .Select(value => (value - meanValue) * (value - meanValue)).Sum();

            double standardDeviation = Math.Sqrt(varianceSum / signalValues.Length);
            if (standardDeviation <= ConstantAlgorithm.Epsilon)
                standardDeviation = 1.0;

            double[] normalizedSignal = new double[signalValues.Length];

            Parallel.For(0, signalValues.Length, index =>
            {
                normalizedSignal[index] = (signalValues[index] - meanValue) / standardDeviation;
            });

            return normalizedSignal;
        }


        private double[] ApplyHampelFilter(double[] signalValues, int windowSize, double sigmaThreshold)
        {
            int signalLength = signalValues.Length;

            if (windowSize < 1) windowSize = 1;
            if (windowSize % 2 == 0) windowSize += 1;

            int halfWindow = windowSize / 2;

            double[] medianValues = ComputeMedianValues(signalValues, halfWindow);
            double[] deviationFromMedian = ComputeDeviationFromMedian(signalValues, medianValues);
            double[] madValues = ComputeMedianOfAbsoluteDeviations(deviationFromMedian, halfWindow);

            double[] thresholdValues = new double[signalLength];

            Parallel.For(0, signalLength, index =>
            {
                thresholdValues[index] =
                    sigmaThreshold *
                    ConstantAlgorithm.MadToStdScale *
                    (madValues[index] + ConstantAlgorithm.Epsilon);
            });

            double[] filteredSignal = (double[])signalValues.Clone();

            Parallel.For(0, signalLength, index =>
            {
                if (Math.Abs(signalValues[index] - medianValues[index]) > thresholdValues[index])
                {
                    filteredSignal[index] = medianValues[index];
                }
            });

            return filteredSignal;
        }


        private double[] ComputeMedianValues(double[] signalValues, int halfWindow)
        {
            int signalLength = signalValues.Length;
            double[] medianValues = new double[signalLength];

            Parallel.For(0, signalLength, index =>
            {
                int startIndex = Math.Max(index - halfWindow, 0);
                int endIndex = Math.Min(index + halfWindow, signalLength - 1);

                double[] windowArray = signalValues
                    .Skip(startIndex)
                    .Take(endIndex - startIndex + 1)
                    .ToArray();
                Array.Sort(windowArray);

                medianValues[index] = windowArray[windowArray.Length / 2];
            });

            return medianValues;
        }


        private double[] ComputeDeviationFromMedian(double[] signalValues, double[] medianValues)
        {
            int signalLength = signalValues.Length;
            double[] deviationArray = new double[signalLength];

            Parallel.For(0, signalLength, index =>
            {
                deviationArray[index] = Math.Abs(signalValues[index] - medianValues[index]);
            });

            return deviationArray;
        }


        private double[] ComputeMedianOfAbsoluteDeviations(double[] deviationValues, int halfWindow)
        {
            int signalLength = deviationValues.Length;
            double[] madValues = new double[signalLength];

            Parallel.For(0, signalLength, index =>
            {
                int startIndex = Math.Max(index - halfWindow, 0);
                int endIndex = Math.Min(index + halfWindow, signalLength - 1);

                double[] windowArray = deviationValues
                    .Skip(startIndex)
                    .Take(endIndex - startIndex + 1)
                    .ToArray();
                Array.Sort(windowArray);

                madValues[index] = windowArray[windowArray.Length / 2];
            });

            return madValues;
        }
    }
}
