using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms.Clustering;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Services.Algorithms.Clustering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Analyzer_Service.Services
{
    public class SegmentClassificationService : ISegmentClassificationService
    {
        private readonly IPrepareFlightData flightDataPreparer;
        private readonly IChangePointDetectionService changePointDetectionService;
        private readonly JsonDocument modelDocument;

        public SegmentClassificationService(
            IPrepareFlightData flightDataPreparer,
            IChangePointDetectionService changePointDetectionService)
        {
            this.flightDataPreparer = flightDataPreparer;
            this.changePointDetectionService = changePointDetectionService;

            string modelJson = File.ReadAllText("Models/Ml/E1_EGT1_rf.json");
            this.modelDocument = JsonDocument.Parse(modelJson);
        }

        public async Task<List<SegmentClassificationResult>> ClassifyAsync(int masterIndex, string fieldName)
        {
            // 1. Load time series and signal from data source
            (List<double> timeSeries, List<double> signalSeries) =
                await flightDataPreparer.PrepareFlightDataAsync(masterIndex, "timestep", fieldName);

            if (timeSeries.Count == 0 || signalSeries.Count == 0 || timeSeries.Count != signalSeries.Count)
            {
                throw new InvalidOperationException("PrepareFlightDataAsync returned invalid series.");
            }

            int sampleCount = signalSeries.Count;

            // 2. Detect change-points (indices are treated as END-EXCLUSIVE boundaries)
            List<int> changePoints =
                await changePointDetectionService.DetectChangePointsAsync(masterIndex, fieldName);

            if (changePoints == null || changePoints.Count == 0)
            {
                return new List<SegmentClassificationResult>();
            }

            changePoints = changePoints
                .Distinct()
                .Where(index => index > 0 && index < sampleCount)
                .OrderBy(index => index)
                .ToList();

            // final boundary should be "len(y)" like בפייתון
            if (!changePoints.Contains(sampleCount))
            {
                changePoints.Add(sampleCount);
            }

            // Build segments as [start, endExclusive)
            List<(int StartIndex, int EndExclusive)> segments =
                BuildSegmentsFromChangePoints(changePoints, sampleCount);

            if (segments.Count == 0)
            {
                return new List<SegmentClassificationResult>();
            }

            // 3. Z-score normalization (this is y_proc / y_z in Python)
            List<double> signalZ = ZScoreNormalize(signalSeries);

            // 4. Precompute mean_z per segment for prev/next features
            List<double> segmentMeanZ = new List<double>(segments.Count);
            foreach ((int startIndex, int endExclusive) in segments)
            {
                double meanZ = Mean(signalZ, startIndex, endExclusive);
                segmentMeanZ.Add(meanZ);
            }

            // 5. Build feature vector per segment and classify with RF
            List<SegmentClassificationResult> results =
                new List<SegmentClassificationResult>(segments.Count);

            for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
            {
                (int startIndex, int endExclusive) = segments[segmentIndex];

                double prevMeanZ = segmentIndex > 0 ? segmentMeanZ[segmentIndex - 1] : 0.0;
                double nextMeanZ = segmentIndex < segmentMeanZ.Count - 1 ? segmentMeanZ[segmentIndex + 1] : 0.0;

                double[] features = ExtractFullFeatures(
                    timeSeries,
                    signalZ,
                    startIndex,
                    endExclusive,
                    prevMeanZ,
                    nextMeanZ);

                string label = MinimalRF.PredictLabel(modelDocument, features);

                SegmentBoundary boundary = new SegmentBoundary(startIndex, endExclusive);
                SegmentClassificationResult result =
                    new SegmentClassificationResult(boundary, label);

                results.Add(result);
            }

            return results;
        }

        // -------------------------------------------------
        // Helpers
        // -------------------------------------------------

        private static List<(int StartIndex, int EndExclusive)> BuildSegmentsFromChangePoints(
            List<int> changePoints,
            int sampleCount)
        {
            List<(int StartIndex, int EndExclusive)> segments =
                new List<(int StartIndex, int EndExclusive)>();

            int currentStartIndex = 0;

            foreach (int boundary in changePoints)
            {
                int endExclusive = Math.Min(boundary, sampleCount);

                if (endExclusive > currentStartIndex + 1)
                {
                    segments.Add((currentStartIndex, endExclusive));
                }

                currentStartIndex = endExclusive;
            }

            return segments;
        }

        private static List<double> ZScoreNormalize(IReadOnlyList<double> values)
        {
            int count = values.Count;

            double sum = 0.0;
            for (int index = 0; index < count; index++)
            {
                sum += values[index];
            }

            double mean = sum / count;

            double sumSquares = 0.0;
            for (int index = 0; index < count; index++)
            {
                double delta = values[index] - mean;
                sumSquares += delta * delta;
            }

            double standardDeviation = Math.Sqrt(sumSquares / count);
            if (standardDeviation < 1e-12)
            {
                standardDeviation = 1.0;
            }

            List<double> normalized = new List<double>(count);
            for (int index = 0; index < count; index++)
            {
                double z = (values[index] - mean) / standardDeviation;
                normalized.Add(z);
            }

            return normalized;
        }

        private static double Mean(IReadOnlyList<double> values, int startIndex, int endExclusive)
        {
            int length = endExclusive - startIndex;
            if (length <= 0)
            {
                return 0.0;
            }

            double sum = 0.0;
            for (int index = startIndex; index < endExclusive; index++)
            {
                sum += values[index];
            }

            return sum / length;
        }

        private static double[] ExtractFullFeatures(
            IReadOnlyList<double> timeSeries,
            IReadOnlyList<double> signalZ,
            int startIndex,
            int endExclusive,
            double prevMeanZ,
            double nextMeanZ)
        {
            int length = endExclusive - startIndex;
            if (length <= 1)
            {
                // still return correct length so the scaler will not break
                return new double[12];
            }

            double tStart = timeSeries[startIndex];
            double tEnd = timeSeries[endExclusive - 1];
            double durationSeconds = Math.Max(tEnd - tStart, 0.0);

            double sum = 0.0;
            double minValue = double.PositiveInfinity;
            double maxValue = double.NegativeInfinity;
            double energySum = 0.0;

            for (int index = startIndex; index < endExclusive; index++)
            {
                double value = signalZ[index];
                sum += value;

                if (value < minValue)
                {
                    minValue = value;
                }

                if (value > maxValue)
                {
                    maxValue = value;
                }

                energySum += value * value;
            }

            double mean = sum / length;

            double varianceSum = 0.0;
            for (int index = startIndex; index < endExclusive; index++)
            {
                double delta = signalZ[index] - mean;
                varianceSum += delta * delta;
            }

            double standardDeviation = Math.Sqrt(varianceSum / length);
            double range = maxValue - minValue;

            double energy = energySum / length;

            double firstValue = signalZ[startIndex];
            double lastValue = signalZ[endExclusive - 1];
            double slope = durationSeconds > 0.0
                ? (lastValue - firstValue) / durationSeconds
                : 0.0;

            // Approximation of scipy.signal.find_peaks with prominence and distance
            int peaksCount = 0;
            int troughsCount = 0;

            if (length >= 3)
            {
                int minimumDistance = Math.Max((int)Math.Floor(0.05 * length), 1);
                int lastPeakIndex = -minimumDistance;
                int lastTroughIndex = -minimumDistance;

                for (int index = startIndex + 1; index < endExclusive - 1; index++)
                {
                    double previous = signalZ[index - 1];
                    double current = signalZ[index];
                    double next = signalZ[index + 1];

                    double localAverage = 0.5 * (previous + next);
                    double prominence = Math.Abs(current - localAverage);

                    bool isPeak = current > previous && current > next && prominence >= 0.5;
                    bool isTrough = current < previous && current < next && prominence >= 0.5;

                    if (isPeak && (index - lastPeakIndex) >= minimumDistance)
                    {
                        peaksCount++;
                        lastPeakIndex = index;
                    }

                    if (isTrough && (index - lastTroughIndex) >= minimumDistance)
                    {
                        troughsCount++;
                        lastTroughIndex = index;
                    }
                }
            }

            // Neighbor means exactly like Python's extract_features (after fillna(0))
            double neighborPrevMean = prevMeanZ;
            double neighborNextMean = nextMeanZ;

            return new double[]
            {
                durationSeconds,     // 0: duration_s
                mean,                // 1: mean_z
                standardDeviation,   // 2: std_z
                minValue,            // 3: min_z
                maxValue,            // 4: max_z
                range,               // 5: range_z
                energy,              // 6: energy_z
                slope,               // 7: slope_mean_z_per_s
                peaksCount,          // 8: n_peaks
                troughsCount,        // 9: n_troughs
                neighborPrevMean,    // 10: dmean_prev (actually mean of previous segment)
                neighborNextMean     // 11: dmean_next (mean of next segment)
            };
        }
    }
}
