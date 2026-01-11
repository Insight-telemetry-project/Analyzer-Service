using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using System;
using System.Collections.Generic;

namespace Analyzer_Service.Services.Algorithms.Pelt
{
    public class FlightPhaseDetector : IFlightPhaseDetector
    {
        // ---- Search windows ----
        private const double TakeoffMustBeBeforePercent = 0.60;
        private const double LandingMustBeAfterPercent = 0.70;

        private const int MinGapBetweenPhases = 50;

        // ---- Stability (slope) ----
        private const double StableAbsSlopeMultiplier = 1.2;
        private const double ClimbSlopeMultiplier = 2.5;
        private const double DescentSlopeMultiplier = 2.5;

        // ---- Duration ----
        private const double TakeoffStableMinSeconds = 120.0;
        private const double LandingStableMinSeconds = 600.0;

        // ---- Level constraints (the key change) ----
        // "How close to cruise mean (in std units) we require for takeoff end"
        private const double TakeoffCruiseStdTolerance = 1.0;

        // "Must reach at least this fraction of the baseline->cruise rise"
        private const double TakeoffRiseFraction = 0.75;

        // Landing: ensure descent is meaningful relative to cruise
        private const double LandingDropStd = 1.0;

        public FlightPhaseIndexes Detect(SegmentAnalysisResult full)
        {
            int flightEndIndex = GetFlightEndIndex(full);

            CruiseStats cruiseStats = ComputeCruiseStats(full, flightEndIndex);
            double baselineMeanZ = ComputeBaselineMeanZ(full);

            double medianAbsSlope = ComputeMedianAbsSlope(full);

            double stableAbsSlopeThreshold = Math.Max(medianAbsSlope * StableAbsSlopeMultiplier, 1e-9);
            double climbSlopeThreshold = Math.Max(medianAbsSlope * ClimbSlopeMultiplier, stableAbsSlopeThreshold * 2.0);
            double descentSlopeThreshold = Math.Max(medianAbsSlope * DescentSlopeMultiplier, stableAbsSlopeThreshold * 2.0);

            int takeoffEndIndex = DetectTakeoffEndIndex(
                full,
                flightEndIndex,
                stableAbsSlopeThreshold,
                climbSlopeThreshold,
                cruiseStats,
                baselineMeanZ);

            int landingStartIndex = DetectLandingStartIndex(
                full,
                flightEndIndex,
                stableAbsSlopeThreshold,
                descentSlopeThreshold,
                cruiseStats,
                takeoffEndIndex);

            if (landingStartIndex <= takeoffEndIndex + MinGapBetweenPhases)
            {
                int fallbackTakeoff = (int)Math.Round(flightEndIndex * 0.20);
                int fallbackLanding = (int)Math.Round(flightEndIndex * 0.80);

                takeoffEndIndex = fallbackTakeoff;
                landingStartIndex = fallbackLanding;
            }

            return new FlightPhaseIndexes(takeoffEndIndex, landingStartIndex);
        }

        private static int DetectTakeoffEndIndex(
            SegmentAnalysisResult full,
            int flightEndIndex,
            double stableAbsSlopeThreshold,
            double climbSlopeThreshold,
            CruiseStats cruiseStats,
            double baselineMeanZ)
        {
            int maxTakeoffIndex = (int)Math.Round(flightEndIndex * TakeoffMustBeBeforePercent);

            // If we couldn't estimate cruise, fallback to old behavior
            if (cruiseStats.HasValue == false)
            {
                return DetectTakeoffByFirstStableAfterClimb(full, maxTakeoffIndex, stableAbsSlopeThreshold, climbSlopeThreshold);
            }

            double cruiseMeanZ = cruiseStats.MeanZ;
            double cruiseStdZ = Math.Max(cruiseStats.StdZ, 1e-9);

            // Require reaching enough of the rise towards cruise
            double requiredLevelByRise =
                baselineMeanZ + (TakeoffRiseFraction * (cruiseMeanZ - baselineMeanZ));

            bool sawRealClimb = false;

            int segmentCount = full.Segments.Count;
            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                SegmentClassificationResult segmentResult = full.Segments[segmentIndex];

                int startIndex = segmentResult.Segment.StartIndex;
                if (startIndex > maxTakeoffIndex)
                {
                    break;
                }

                SegmentFeatures features = segmentResult.FeatureValues;
                double slope = features != null ? features.Slope : 0.0;

                if (slope >= climbSlopeThreshold)
                {
                    sawRealClimb = true;
                }

                if (sawRealClimb == false)
                {
                    continue;
                }

                if (IsStableLabel(segmentResult.Label) == false)
                {
                    continue;
                }

                if (features == null)
                {
                    continue;
                }

                double durationSeconds = features.DurationSeconds;
                if (durationSeconds < TakeoffStableMinSeconds)
                {
                    continue;
                }

                double absSlope = Math.Abs(features.Slope);
                if (absSlope > stableAbsSlopeThreshold)
                {
                    continue;
                }

                double candidateMeanZ = features.MeanZ;

                // 1) close to cruise mean (std-based)
                double distanceToCruiseStd = Math.Abs(candidateMeanZ - cruiseMeanZ) / cruiseStdZ;
                if (distanceToCruiseStd > TakeoffCruiseStdTolerance)
                {
                    continue;
                }

                // 2) reached enough of the rise towards cruise (prevents "too early" takeoff end)
                if (candidateMeanZ < requiredLevelByRise)
                {
                    continue;
                }

                // Takeoff end boundary = start of that stable region
                return Math.Max(0, segmentResult.Segment.StartIndex);
            }

            // If nothing matched (rare), fallback to strongest early positive slope end
            int fallbackEndIndex = FindStrongestPositiveSlopeEnd(full, maxTakeoffIndex);
            return Math.Max(0, fallbackEndIndex);
        }

        private static int DetectLandingStartIndex(
            SegmentAnalysisResult full,
            int flightEndIndex,
            double stableAbsSlopeThreshold,
            double descentSlopeThreshold,
            CruiseStats cruiseStats,
            int takeoffEndIndex)
        {
            int landingSearchStartIndex = (int)Math.Round(flightEndIndex * LandingMustBeAfterPercent);

            int segmentCount = full.Segments.Count;

            for (int segmentIndex = segmentCount - 1; segmentIndex >= 0; segmentIndex--)
            {
                SegmentClassificationResult stableCandidate = full.Segments[segmentIndex];

                int stableEndIndex = stableCandidate.Segment.EndIndex;
                if (stableEndIndex < landingSearchStartIndex)
                {
                    break;
                }

                if (IsStableLabel(stableCandidate.Label) == false)
                {
                    continue;
                }

                SegmentFeatures stableFeatures = stableCandidate.FeatureValues;
                if (stableFeatures == null)
                {
                    continue;
                }

                if (stableFeatures.DurationSeconds < LandingStableMinSeconds)
                {
                    continue;
                }

                double stableAbsSlope = Math.Abs(stableFeatures.Slope);
                if (stableAbsSlope > stableAbsSlopeThreshold)
                {
                    continue;
                }

                // Candidate last cruise-like stable found.
                int nextIndex = segmentIndex + 1;
                if (nextIndex >= segmentCount)
                {
                    return Math.Max(stableEndIndex, takeoffEndIndex + MinGapBetweenPhases);
                }

                SegmentClassificationResult nextSegment = full.Segments[nextIndex];

                bool isRampDown = string.Equals(nextSegment.Label, "RampDown", StringComparison.OrdinalIgnoreCase);

                SegmentFeatures nextFeatures = nextSegment.FeatureValues;
                double nextSlope = nextFeatures != null ? nextFeatures.Slope : 0.0;

                bool isStrongDescentBySlope = nextSlope <= -descentSlopeThreshold;

                bool isMeaningfulDropByLevel = false;
                if (cruiseStats.HasValue && nextFeatures != null)
                {
                    double cruiseStdZ = Math.Max(cruiseStats.StdZ, 1e-9);
                    double cruiseMeanZ = cruiseStats.MeanZ;

                    // require drop from cruise mean by >= LandingDropStd * std
                    isMeaningfulDropByLevel = nextFeatures.MeanZ <= (cruiseMeanZ - (LandingDropStd * cruiseStdZ));
                }

                int landingStartIndex = nextSegment.Segment.StartIndex;

                if (isRampDown || isStrongDescentBySlope || isMeaningfulDropByLevel)
                {
                    return Math.Max(landingStartIndex, takeoffEndIndex + MinGapBetweenPhases);
                }

                // If we didn't see a "real" descent right after, start landing after the last long stable
                return Math.Max(stableEndIndex, takeoffEndIndex + MinGapBetweenPhases);
            }

            int fallbackLate = (int)Math.Round(flightEndIndex * 0.85);
            return Math.Max(fallbackLate, takeoffEndIndex + MinGapBetweenPhases);
        }

        // ---------------- helpers ----------------

        private static bool IsStableLabel(string label)
        {
            return
                string.Equals(label, "Steady", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(label, "Neutral", StringComparison.OrdinalIgnoreCase);
        }

        private static int GetFlightEndIndex(SegmentAnalysisResult full)
        {
            int lastSegmentIndex = full.Segments.Count - 1;
            return full.Segments[lastSegmentIndex].Segment.EndIndex;
        }

        private static int DetectTakeoffByFirstStableAfterClimb(
            SegmentAnalysisResult full,
            int maxTakeoffIndex,
            double stableAbsSlopeThreshold,
            double climbSlopeThreshold)
        {
            bool sawRealClimb = false;

            int segmentCount = full.Segments.Count;
            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                SegmentClassificationResult segmentResult = full.Segments[segmentIndex];

                int startIndex = segmentResult.Segment.StartIndex;
                if (startIndex > maxTakeoffIndex)
                {
                    break;
                }

                SegmentFeatures features = segmentResult.FeatureValues;
                double slope = features != null ? features.Slope : 0.0;

                if (slope >= climbSlopeThreshold)
                {
                    sawRealClimb = true;
                }

                if (sawRealClimb == false)
                {
                    continue;
                }

                if (IsStableLabel(segmentResult.Label) == false)
                {
                    continue;
                }

                if (features == null)
                {
                    continue;
                }

                if (features.DurationSeconds < TakeoffStableMinSeconds)
                {
                    continue;
                }

                if (Math.Abs(features.Slope) > stableAbsSlopeThreshold)
                {
                    continue;
                }

                return Math.Max(0, segmentResult.Segment.StartIndex);
            }

            int fallbackEndIndex = FindStrongestPositiveSlopeEnd(full, maxTakeoffIndex);
            return Math.Max(0, fallbackEndIndex);
        }

        private static int FindStrongestPositiveSlopeEnd(SegmentAnalysisResult full, int maxIndexAllowed)
        {
            double bestScore = double.NegativeInfinity;
            int bestEndIndex = 0;

            int segmentCount = full.Segments.Count;
            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                SegmentClassificationResult segmentResult = full.Segments[segmentIndex];

                int startIndex = segmentResult.Segment.StartIndex;
                int endIndex = segmentResult.Segment.EndIndex;

                if (startIndex > maxIndexAllowed)
                {
                    break;
                }

                SegmentFeatures features = segmentResult.FeatureValues;

                double slope = features != null ? features.Slope : 0.0;
                double durationSeconds = features != null ? features.DurationSeconds : Math.Max(1, endIndex - startIndex);
                double rangeZ = features != null ? features.RangeZ : 0.0;

                double positiveSlope = Math.Max(0.0, slope);
                double score = positiveSlope * Math.Max(1.0, durationSeconds) * Math.Max(0.001, rangeZ);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestEndIndex = endIndex;
                }
            }

            return bestEndIndex;
        }

        private static double ComputeBaselineMeanZ(SegmentAnalysisResult full)
        {
            // simplest robust baseline: first segment MeanZ if exists
            SegmentClassificationResult firstSegment = full.Segments[0];
            SegmentFeatures firstFeatures = firstSegment.FeatureValues;

            if (firstFeatures == null)
            {
                return 0.0;
            }

            return firstFeatures.MeanZ;
        }

        private static CruiseStats ComputeCruiseStats(SegmentAnalysisResult full, int flightEndIndex)
        {
            double midStart = flightEndIndex * 0.25;
            double midEnd = flightEndIndex * 0.75;

            double bestDuration = double.NegativeInfinity;
            double bestMeanZ = 0.0;
            double bestStdZ = 0.0;

            int segmentCount = full.Segments.Count;
            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                SegmentClassificationResult segmentResult = full.Segments[segmentIndex];

                if (IsStableLabel(segmentResult.Label) == false)
                {
                    continue;
                }

                int startIndex = segmentResult.Segment.StartIndex;
                int endIndex = segmentResult.Segment.EndIndex;

                if (endIndex < midStart || startIndex > midEnd)
                {
                    continue;
                }

                SegmentFeatures features = segmentResult.FeatureValues;
                if (features == null)
                {
                    continue;
                }

                double durationSeconds = features.DurationSeconds;
                if (durationSeconds > bestDuration)
                {
                    bestDuration = durationSeconds;
                    bestMeanZ = features.MeanZ;
                    bestStdZ = features.StdZ;
                }
            }

            if (bestDuration <= 0.0)
            {
                return CruiseStats.Empty();
            }

            return CruiseStats.Create(bestMeanZ, bestStdZ);
        }

        private static double ComputeMedianAbsSlope(SegmentAnalysisResult full)
        {
            List<double> absSlopes = new List<double>();

            int segmentCount = full.Segments.Count;
            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                SegmentFeatures features = full.Segments[segmentIndex].FeatureValues;
                if (features == null)
                {
                    continue;
                }

                double slope = features.Slope;
                if (double.IsNaN(slope) || double.IsInfinity(slope))
                {
                    continue;
                }

                absSlopes.Add(Math.Abs(slope));
            }

            if (absSlopes.Count == 0)
            {
                return 0.0;
            }

            absSlopes.Sort();
            int n = absSlopes.Count;

            if (n % 2 == 1)
            {
                return absSlopes[n / 2];
            }

            double a = absSlopes[(n / 2) - 1];
            double b = absSlopes[n / 2];
            return (a + b) / 2.0;
        }

        private readonly struct CruiseStats
        {
            public bool HasValue { get; }
            public double MeanZ { get; }
            public double StdZ { get; }

            private CruiseStats(bool hasValue, double meanZ, double stdZ)
            {
                HasValue = hasValue;
                MeanZ = meanZ;
                StdZ = stdZ;
            }

            public static CruiseStats Create(double meanZ, double stdZ)
            {
                return new CruiseStats(true, meanZ, stdZ);
            }

            public static CruiseStats Empty()
            {
                return new CruiseStats(false, 0.0, 0.0);
            }
        }
    }
}
