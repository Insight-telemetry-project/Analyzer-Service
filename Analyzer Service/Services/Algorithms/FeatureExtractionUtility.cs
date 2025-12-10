using Analyzer_Service.Models.Dto;
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

        public List<SegmentBoundary> BuildSegmentsFromPoints(List<int> boundaries,int sampleCount)
        {
            List<SegmentBoundary> segments = new List<SegmentBoundary>();

            int currentStart = 0;

            for (int index = 0; index < boundaries.Count; index++)
            {
                int boundary = boundaries[index];
                int endExclusive = boundary;

                if (endExclusive > currentStart + 1)
                {
                    SegmentBoundary segment = new SegmentBoundary(currentStart, endExclusive);
                    segments.Add(segment);
                }

                currentStart = endExclusive;
            }

            return segments;
        }


        public SegmentFeatures ExtractFeatures(List<double> timeSeries,List<double> processedSignal,SegmentBoundary segment,double previousMean,double nextMean)
        {
            int startIndex = segment.StartIndex;
            int endIndex = segment.EndIndex;

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

            return new SegmentFeatures
            {
                DurationSeconds = duration,
                MeanZ = mean,
                StdZ = std,
                MinZ = minValue,
                MaxZ = maxValue,
                RangeZ = range,
                EnergyZ = energy,
                Slope = slope,
                PeakCount = peakCount,
                TroughCount = troughCount,
                MeanPrev = validatedPrev,
                MeanNext = validatedNext
            };

        }


        public int CountPeaks(List<double> signal, int startIndex, int endIndex)
        {
            int length = endIndex - startIndex;
            int minimumDistance = Math.Max((int)Math.Floor(0.05 * length), 1);

            int count = 0;
            int lastIndex = -minimumDistance;

            for (int index = startIndex + 1; index < endIndex - 1; index++)
            {
                double prev = signal[index - 1];
                double current = signal[index];
                double next = signal[index + 1];

                double prominence = Math.Abs(current - 0.5 * (prev + next));

                bool isPeak = current > prev && current > next && prominence >= 0.5;

                if (isPeak && (index - lastIndex) >= minimumDistance)
                {
                    count++;
                    lastIndex = index;
                }
            }

            return count;
        }

        public int CountTroughs(List<double> signal, int startIndex, int endIndex)
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
