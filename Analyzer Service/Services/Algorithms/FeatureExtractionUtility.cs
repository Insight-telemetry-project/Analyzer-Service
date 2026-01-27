using System;
using System.Collections.Generic;
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

        public List<SegmentBoundary> BuildSegmentsFromPoints(List<int> boundaries, int sampleCount)
        {
            List<SegmentBoundary> segments = new List<SegmentBoundary>();

            int currentStartIndex = 0;

            for (int boundaryIndex = 0; boundaryIndex < boundaries.Count; boundaryIndex++)
            {
                int endExclusiveIndex = boundaries[boundaryIndex];

                if (endExclusiveIndex > currentStartIndex + 1)
                {
                    SegmentBoundary segmentBoundary = new SegmentBoundary(currentStartIndex, endExclusiveIndex);
                    segments.Add(segmentBoundary);
                }

                currentStartIndex = endExclusiveIndex;
            }

            return segments;
        }

        public SegmentFeatures ExtractFeatures(
            double[] timeSeriesValues,
            double[] processedSignalValues,
            SegmentBoundary segmentBoundary,
            double previousMean,
            double nextMean)
        {
            int startIndex = segmentBoundary.StartIndex;
            int endIndex = segmentBoundary.EndIndex;

            int segmentLength = endIndex - startIndex;

            double startTime = timeSeriesValues[startIndex];
            double endTime = timeSeriesValues[endIndex - 1];
            double durationSeconds = endTime - startTime;

            double minValue = double.PositiveInfinity;
            double maxValue = double.NegativeInfinity;
            double sum = 0.0;
            double energySum = 0.0;

            for (int sampleIndex = startIndex; sampleIndex < endIndex; sampleIndex++)
            {
                double value = processedSignalValues[sampleIndex];

                sum += value;
                energySum += value * value;

                if (value < minValue)
                {
                    minValue = value;
                }

                if (value > maxValue)
                {
                    maxValue = value;
                }
            }

            double mean = sum / segmentLength;
            double range = maxValue - minValue;
            double energy = energySum / segmentLength;

            double varianceSum = 0.0;
            for (int sampleIndex = startIndex; sampleIndex < endIndex; sampleIndex++)
            {
                double delta = processedSignalValues[sampleIndex] - mean;
                varianceSum += delta * delta;
            }

            double std = Math.Sqrt(varianceSum / segmentLength);

            double firstValue = processedSignalValues[startIndex];
            double lastValue = processedSignalValues[endIndex - 1];

            double slope = durationSeconds > 0.0
                ? (lastValue - firstValue) / durationSeconds
                : 0.0;

            int peakCount = CountPeaks(processedSignalValues, startIndex, endIndex);
            int troughCount = CountTroughs(processedSignalValues, startIndex, endIndex);

            double safePreviousMean = double.IsNaN(previousMean) ? 0.0 : previousMean;
            double safeNextMean = double.IsNaN(nextMean) ? 0.0 : nextMean;

            SegmentFeatures segmentFeatures = new SegmentFeatures
            {
                DurationSeconds = durationSeconds,
                MeanZ = mean,
                StdZ = std,
                MinZ = minValue,
                MaxZ = maxValue,
                RangeZ = range,
                EnergyZ = energy,
                Slope = slope,
                PeakCount = peakCount,
                TroughCount = troughCount,
                MeanPrev = safePreviousMean,
                MeanNext = safeNextMean
            };

            return segmentFeatures;
        }

        public int CountPeaks(double[] signalValues, int startIndex, int endIndex)
        {
            int length = endIndex - startIndex;
            int minimumDistance = Math.Max((int)Math.Floor(0.05 * length), 1);

            int count = 0;
            int lastIndex = -minimumDistance;

            for (int index = startIndex + 1; index < endIndex - 1; index++)
            {
                double prev = signalValues[index - 1];
                double current = signalValues[index];
                double next = signalValues[index + 1];

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

        public int CountTroughs(double[] signalValues, int startIndex, int endIndex)
        {
            int length = endIndex - startIndex;
            int minimumDistance = Math.Max((int)Math.Floor(0.05 * length), 1);

            int count = 0;
            int lastIndex = -minimumDistance;

            for (int index = startIndex + 1; index < endIndex - 1; index++)
            {
                double prev = signalValues[index - 1];
                double current = signalValues[index];
                double next = signalValues[index + 1];

                double prominence = Math.Abs(current - 0.5 * (prev + next));
                bool isTrough = current < prev && current < next && prominence >= 0.5;

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
