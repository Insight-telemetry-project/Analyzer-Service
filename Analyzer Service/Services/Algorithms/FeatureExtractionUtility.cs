using Analyzer_Service.Models.Interface.Algorithms;

namespace Analyzer_Service.Services.Algorithms
{
    public class FeatureExtractionUtility : IFeatureExtractionUtility
    {
        private readonly ISignalProcessingUtility signalProcessingUtility;

        public FeatureExtractionUtility(ISignalProcessingUtility signalProcessingUtility)
        {
            this.signalProcessingUtility = signalProcessingUtility;
        }

        public List<(int StartIndex, int EndIndex)> BuildSegments(
            List<int> boundaries,
            int sampleCount)
        {
            List<(int StartIndex, int EndIndex)> segments =
                new List<(int StartIndex, int EndIndex)>();

            int currentStart = 0;

            for (int i = 0; i < boundaries.Count; i++)
            {
                int boundary = boundaries[i];
                int endExclusive = boundary;

                if (endExclusive > currentStart + 1)
                {
                    segments.Add((currentStart, endExclusive));
                }

                currentStart = endExclusive;
            }

            return segments;
        }

        public double[] ExtractFeatures(
            IReadOnlyList<double> timeSeries,
            IReadOnlyList<double> processedSignal,
            int startIndex,
            int endIndex,
            double previousMean,
            double nextMean)
        {
            int length = endIndex - startIndex;

            double startTime = timeSeries[startIndex];
            double endTime = timeSeries[endIndex - 1];
            double duration = endTime - startTime;

            double minValue = double.PositiveInfinity;
            double maxValue = double.NegativeInfinity;
            double sum = 0.0;
            double energySum = 0.0;

            for (int index = startIndex; index < endIndex; index++)
            {
                double value = processedSignal[index];

                sum += value;
                energySum += value * value;

                if (value < minValue) minValue = value;
                if (value > maxValue) maxValue = value;
            }

            double mean = sum / length;
            double range = maxValue - minValue;
            double energy = energySum / length;

            double varianceSum = 0.0;
            for (int index = startIndex; index < endIndex; index++)
            {
                double delta = processedSignal[index] - mean;
                varianceSum += delta * delta;
            }
            double std = Math.Sqrt(varianceSum / length);

            double firstValue = processedSignal[startIndex];
            double lastValue = processedSignal[endIndex - 1];
            double slope = duration > 0.0
                ? (lastValue - firstValue) / duration
                : 0.0;

            int peakCount = CountPeaks(processedSignal, startIndex, endIndex);
            int troughCount = CountTroughs(processedSignal, startIndex, endIndex);

            double validatedPrev = double.IsNaN(previousMean) ? 0.0 : previousMean;
            double validatedNext = double.IsNaN(nextMean) ? 0.0 : nextMean;

            return new double[]
            {
                duration,
                mean,
                std,
                minValue,
                maxValue,
                range,
                energy,
                slope,
                peakCount,
                troughCount,
                validatedPrev,
                validatedNext
            };
        }

        public int CountPeaks(IReadOnlyList<double> signal, int startIndex, int endIndex)
        {
            int length = endIndex - startIndex;
            int minimumDistance = Math.Max((int)Math.Floor(0.05 * length), 1);

            int count = 0;
            int lastIndex = -minimumDistance;

            for (int index = startIndex + 1; index < endIndex - 1; index++)
            {
                double prev = signal[index - 1];
                double curr = signal[index];
                double next = signal[index + 1];

                double prominence = Math.Abs(curr - 0.5 * (prev + next));

                bool isPeak = curr > prev && curr > next && prominence >= 0.5;

                if (isPeak && (index - lastIndex) >= minimumDistance)
                {
                    count++;
                    lastIndex = index;
                }
            }

            return count;
        }

        public int CountTroughs(IReadOnlyList<double> signal, int startIndex, int endIndex)
        {
            int length = endIndex - startIndex;
            int minimumDistance = Math.Max((int)Math.Floor(0.05 * length), 1);

            int count = 0;
            int lastIndex = -minimumDistance;

            for (int index = startIndex + 1; index < endIndex - 1; index++)
            {
                double prev = signal[index - 1];
                double curr = signal[index];
                double next = signal[index + 1];

                double prominence = Math.Abs(curr - 0.5 * (prev + next));

                bool isTrough = curr < prev && curr < next && prominence >= 0.5;

                if (isTrough && (index - lastIndex) >= minimumDistance)
                {
                    count++;
                    lastIndex = index;
                }
            }

            return count;
        }
    }
}
