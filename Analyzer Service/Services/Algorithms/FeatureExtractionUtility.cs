using System;
using System.Collections.Generic;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms;

namespace Analyzer_Service.Services.Algorithms
{
    public class FeatureExtractionUtility : IFeatureExtractionUtility
    {
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
            IReadOnlyList<double> processedSignalValues,
            SegmentBoundary segmentBoundary,
            double previousMean,
            double nextMean)
        {
            int startIndex = segmentBoundary.StartIndex;
            int endIndex = segmentBoundary.EndIndex;

            int segmentLength = endIndex - startIndex;

            
            double durationSeconds = (endIndex - 1) - startIndex;

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
                MeanPrev = previousMean,
                MeanNext = nextMean
            };

            return segmentFeatures;
        }

        public int CountPeaks(IReadOnlyList<double> signalValues, int startIndex, int endIndex)
        {
            int length = endIndex - startIndex;
            int minimumDistance = Math.Max((int)Math.Floor(0.05 * length), 1);

            int peakCount = 0;
            int lastPeakIndex = -minimumDistance;

            for (int sampleIndex = startIndex + 1; sampleIndex < endIndex - 1; sampleIndex++)
            {
                double previousValue = signalValues[sampleIndex - 1];
                double currentValue = signalValues[sampleIndex];
                double nextValue = signalValues[sampleIndex + 1];

                double prominence = Math.Abs(currentValue - 0.5 * (previousValue + nextValue));
                bool isPeak = currentValue > previousValue && currentValue > nextValue && prominence >= 0.5;

                if (isPeak && (sampleIndex - lastPeakIndex) >= minimumDistance)
                {
                    peakCount++;
                    lastPeakIndex = sampleIndex;
                }
            }

            return peakCount;
        }

        public int CountTroughs(IReadOnlyList<double> signalValues, int startIndex, int endIndex)
        {
            int length = endIndex - startIndex;
            int minimumDistance = Math.Max((int)Math.Floor(0.05 * length), 1);

            int troughCount = 0;
            int lastTroughIndex = -minimumDistance;

            for (int sampleIndex = startIndex + 1; sampleIndex < endIndex - 1; sampleIndex++)
            {
                double previousValue = signalValues[sampleIndex - 1];
                double currentValue = signalValues[sampleIndex];
                double nextValue = signalValues[sampleIndex + 1];

                double prominence = Math.Abs(currentValue - 0.5 * (previousValue + nextValue));
                bool isTrough = currentValue < previousValue && currentValue < nextValue && prominence >= 0.5;

                if (isTrough && (sampleIndex - lastTroughIndex) >= minimumDistance)
                {
                    troughCount++;
                    lastTroughIndex = sampleIndex;
                }
            }

            return troughCount;
        }
    }
}
