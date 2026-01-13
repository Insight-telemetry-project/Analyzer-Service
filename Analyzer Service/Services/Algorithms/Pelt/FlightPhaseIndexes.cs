using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using System;
using System.Collections.Generic;

namespace Analyzer_Service.Services.Algorithms.Pelt
{
    public class FlightPhaseDetector : IFlightPhaseDetector
    {
        private const double TakeoffMustBeBeforePercent = 0.60;
        private const double LandingMustBeAfterPercent = 0.70;

        private const int MinGapBetweenPhases = 50;

        private const double StableAbsSlopeMultiplier = 1.2;
        private const double ClimbSlopeMultiplier = 2.5;
        private const double DescentSlopeMultiplier = 2.5;

        private const double TakeoffStableMinSeconds = 120.0;
        private const double LandingStableMinSeconds = 600.0;

        private const double TakeoffCruiseStdTolerance = 1.0;
        private const double TakeoffRiseFraction = 0.75;
        private const double LandingDropStd = 1.0;

        public FlightPhaseIndexes Detect(SegmentAnalysisResult fullResult)
        {
            int flightEndIndex = GetFlightEndIndex(fullResult);

            (bool hasCruiseStats, double cruiseMeanZ, double cruiseStdZ) cruiseStatsResult =
                ComputeCruiseStats(fullResult, flightEndIndex);

            double baselineMeanZ = ComputeBaselineMeanZ(fullResult);

            double medianAbsSlope = ComputeMedianAbsSlope(fullResult);

            double stableAbsSlopeThreshold = Math.Max(medianAbsSlope * StableAbsSlopeMultiplier, 1e-9);
            double climbSlopeThreshold = Math.Max(medianAbsSlope * ClimbSlopeMultiplier, stableAbsSlopeThreshold * 2.0);
            double descentSlopeThreshold = Math.Max(medianAbsSlope * DescentSlopeMultiplier, stableAbsSlopeThreshold * 2.0);

            int takeoffEndIndex = DetectTakeoffEndIndex(
                fullResult,
                flightEndIndex,
                stableAbsSlopeThreshold,
                climbSlopeThreshold,
                cruiseStatsResult.hasCruiseStats,
                cruiseStatsResult.cruiseMeanZ,
                cruiseStatsResult.cruiseStdZ,
                baselineMeanZ);

            int landingStartIndex = DetectLandingStartIndex(
                fullResult,
                flightEndIndex,
                stableAbsSlopeThreshold,
                descentSlopeThreshold,
                cruiseStatsResult.hasCruiseStats,
                cruiseStatsResult.cruiseMeanZ,
                cruiseStatsResult.cruiseStdZ,
                takeoffEndIndex);

            if (landingStartIndex <= takeoffEndIndex + MinGapBetweenPhases)
            {
                int fallbackTakeoffEndIndex = (int)Math.Round(flightEndIndex * 0.20);
                int fallbackLandingStartIndex = (int)Math.Round(flightEndIndex * 0.80);

                takeoffEndIndex = fallbackTakeoffEndIndex;
                landingStartIndex = fallbackLandingStartIndex;
            }

            return new FlightPhaseIndexes(takeoffEndIndex, landingStartIndex);
        }

        private int DetectTakeoffEndIndex(
            SegmentAnalysisResult fullResult,
            int flightEndIndex,
            double stableAbsSlopeThreshold,
            double climbSlopeThreshold,
            bool hasCruiseStats,
            double cruiseMeanZ,
            double cruiseStdZ,
            double baselineMeanZ)
        {
            int maxTakeoffIndex = (int)Math.Round(flightEndIndex * TakeoffMustBeBeforePercent);

            if (hasCruiseStats == false)
            {
                return DetectTakeoffByFirstStableAfterClimb(fullResult, maxTakeoffIndex, stableAbsSlopeThreshold, climbSlopeThreshold);
            }

            double safeCruiseStdZ = Math.Max(cruiseStdZ, 1e-9);

            double requiredLevelByRise =
                baselineMeanZ + (TakeoffRiseFraction * (cruiseMeanZ - baselineMeanZ));

            bool sawRealClimb = false;

            int segmentCount = fullResult.Segments.Count;
            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                SegmentClassificationResult segmentResult = fullResult.Segments[segmentIndex];

                int segmentStartIndex = segmentResult.Segment.StartIndex;
                if (segmentStartIndex > maxTakeoffIndex)
                {
                    break;
                }

                SegmentFeatures segmentFeatures = segmentResult.FeatureValues;

                double segmentSlope = segmentFeatures.Slope;
                if (segmentSlope >= climbSlopeThreshold)
                {
                    sawRealClimb = true;
                }

                if (IsValidTakeoffEndCandidate(
                        sawRealClimb,
                        segmentResult,
                        segmentFeatures,
                        stableAbsSlopeThreshold,
                        cruiseMeanZ,
                        safeCruiseStdZ,
                        requiredLevelByRise) == false)
                {
                    continue;
                }

                return Math.Max(0, segmentResult.Segment.StartIndex);
            }

            int fallbackEndIndex = FindStrongestPositiveSlopeEnd(fullResult, maxTakeoffIndex);
            return Math.Max(0, fallbackEndIndex);
        }

        private bool IsValidTakeoffEndCandidate(
            bool sawRealClimb,
            SegmentClassificationResult segmentResult,
            SegmentFeatures segmentFeatures,
            double stableAbsSlopeThreshold,
            double cruiseMeanZ,
            double safeCruiseStdZ,
            double requiredLevelByRise)
        {
            bool isStable = IsStableLabel(segmentResult.Label);

            double durationSeconds = segmentFeatures.DurationSeconds;
            double absSlope = Math.Abs(segmentFeatures.Slope);
            double candidateMeanZ = segmentFeatures.MeanZ;

            double distanceToCruiseStd = Math.Abs(candidateMeanZ - cruiseMeanZ) / safeCruiseStdZ;

            bool isValid =
                sawRealClimb &&
                isStable &&
                durationSeconds >= TakeoffStableMinSeconds &&
                absSlope <= stableAbsSlopeThreshold &&
                distanceToCruiseStd <= TakeoffCruiseStdTolerance &&
                candidateMeanZ >= requiredLevelByRise;

            return isValid;
        }

        private int DetectLandingStartIndex(
            SegmentAnalysisResult fullResult,
            int flightEndIndex,
            double stableAbsSlopeThreshold,
            double descentSlopeThreshold,
            bool hasCruiseStats,
            double cruiseMeanZ,
            double cruiseStdZ,
            int takeoffEndIndex)
        {
            int landingSearchStartIndex = (int)Math.Round(flightEndIndex * LandingMustBeAfterPercent);

            int segmentCount = fullResult.Segments.Count;

            for (int segmentIndex = segmentCount - 1; segmentIndex >= 0; segmentIndex--)
            {
                SegmentClassificationResult stableCandidate = fullResult.Segments[segmentIndex];

                int stableEndIndex = stableCandidate.Segment.EndIndex;
                if (stableEndIndex < landingSearchStartIndex)
                {
                    break;
                }

                SegmentFeatures stableFeatures = stableCandidate.FeatureValues;

                if (IsValidLandingStableCandidate(stableCandidate, stableFeatures, stableAbsSlopeThreshold) == false)
                {
                    continue;
                }

                int nextSegmentIndex = segmentIndex + 1;
                if (nextSegmentIndex >= segmentCount)
                {
                    return Math.Max(stableEndIndex, takeoffEndIndex + MinGapBetweenPhases);
                }

                SegmentClassificationResult nextSegment = fullResult.Segments[nextSegmentIndex];
                SegmentFeatures nextFeatures = nextSegment.FeatureValues;

                bool isRampDown = string.Equals(nextSegment.Label, ConstantRandomForest.RAMP_DOWN, StringComparison.OrdinalIgnoreCase);
                bool isStrongDescentBySlope = nextFeatures.Slope <= -descentSlopeThreshold;

                bool isMeaningfulDropByLevel = false;
                if (hasCruiseStats)
                {
                    double safeCruiseStdZ = Math.Max(cruiseStdZ, 1e-9);
                    isMeaningfulDropByLevel = nextFeatures.MeanZ <= (cruiseMeanZ - (LandingDropStd * safeCruiseStdZ));
                }

                int landingStartIndex = nextSegment.Segment.StartIndex;

                if (isRampDown || isStrongDescentBySlope || isMeaningfulDropByLevel)
                {
                    return Math.Max(landingStartIndex, takeoffEndIndex + MinGapBetweenPhases);
                }

                return Math.Max(stableEndIndex, takeoffEndIndex + MinGapBetweenPhases);
            }

            int fallbackLandingStartIndex = (int)Math.Round(flightEndIndex * 0.85);
            return Math.Max(fallbackLandingStartIndex, takeoffEndIndex + MinGapBetweenPhases);
        }

        private bool IsValidLandingStableCandidate(
            SegmentClassificationResult stableCandidate,
            SegmentFeatures stableFeatures,
            double stableAbsSlopeThreshold)
        {
            bool isStable = IsStableLabel(stableCandidate.Label);
            double durationSeconds = stableFeatures.DurationSeconds;
            double absSlope = Math.Abs(stableFeatures.Slope);

            bool isValid =
                isStable &&
                durationSeconds >= LandingStableMinSeconds &&
                absSlope <= stableAbsSlopeThreshold;

            return isValid;
        }

        private bool IsStableLabel(string label)
        {
            return
                string.Equals(label, ConstantRandomForest.STEADY, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(label, ConstantRandomForest.NEUTRAL, StringComparison.OrdinalIgnoreCase);
        }

        private int GetFlightEndIndex(SegmentAnalysisResult fullResult)
        {
            int lastSegmentIndex = fullResult.Segments.Count - 1;
            return fullResult.Segments[lastSegmentIndex].Segment.EndIndex;
        }

        private int DetectTakeoffByFirstStableAfterClimb(
            SegmentAnalysisResult fullResult,
            int maxTakeoffIndex,
            double stableAbsSlopeThreshold,
            double climbSlopeThreshold)
        {
            bool sawRealClimb = false;

            int segmentCount = fullResult.Segments.Count;
            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                SegmentClassificationResult segmentResult = fullResult.Segments[segmentIndex];

                int segmentStartIndex = segmentResult.Segment.StartIndex;
                if (segmentStartIndex > maxTakeoffIndex)
                {
                    break;
                }

                SegmentFeatures segmentFeatures = segmentResult.FeatureValues;

                double segmentSlope = segmentFeatures.Slope;
                if (segmentSlope >= climbSlopeThreshold)
                {
                    sawRealClimb = true;
                }

                if (IsValidTakeoffFallbackCandidate(
                        sawRealClimb,
                        segmentResult,
                        segmentFeatures,
                        stableAbsSlopeThreshold) == false)
                {
                    continue;
                }

                return Math.Max(0, segmentResult.Segment.StartIndex);
            }

            int fallbackEndIndex = FindStrongestPositiveSlopeEnd(fullResult, maxTakeoffIndex);
            return Math.Max(0, fallbackEndIndex);
        }

        private bool IsValidTakeoffFallbackCandidate(
            bool sawRealClimb,
            SegmentClassificationResult segmentResult,
            SegmentFeatures segmentFeatures,
            double stableAbsSlopeThreshold)
        {
            bool isStable = IsStableLabel(segmentResult.Label);

            double durationSeconds = segmentFeatures.DurationSeconds;
            double absSlope = Math.Abs(segmentFeatures.Slope);

            bool isValid =
                sawRealClimb &&
                isStable &&
                durationSeconds >= TakeoffStableMinSeconds &&
                absSlope <= stableAbsSlopeThreshold;

            return isValid;
        }

        private int FindStrongestPositiveSlopeEnd(SegmentAnalysisResult fullResult, int maxIndexAllowed)
        {
            double bestScore = double.NegativeInfinity;
            int bestEndIndex = 0;

            int segmentCount = fullResult.Segments.Count;
            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                SegmentClassificationResult segmentResult = fullResult.Segments[segmentIndex];

                int segmentStartIndex = segmentResult.Segment.StartIndex;
                int segmentEndIndex = segmentResult.Segment.EndIndex;

                if (segmentStartIndex > maxIndexAllowed)
                {
                    break;
                }

                SegmentFeatures segmentFeatures = segmentResult.FeatureValues;

                double segmentSlope = segmentFeatures.Slope;
                double durationSeconds = Math.Max(1.0, segmentFeatures.DurationSeconds);
                double rangeZ = segmentFeatures.RangeZ;

                double positiveSlope = Math.Max(0.0, segmentSlope);
                double score = positiveSlope * Math.Max(1.0, durationSeconds) * Math.Max(0.001, rangeZ);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestEndIndex = segmentEndIndex;
                }
            }

            return bestEndIndex;
        }

        private double ComputeBaselineMeanZ(SegmentAnalysisResult fullResult)
        {
            SegmentClassificationResult firstSegment = fullResult.Segments[0];
            SegmentFeatures firstFeatures = firstSegment.FeatureValues;

            return firstFeatures.MeanZ;
        }

        private (bool hasCruiseStats, double cruiseMeanZ, double cruiseStdZ) ComputeCruiseStats(
            SegmentAnalysisResult fullResult,
            int flightEndIndex)
        {
            double midStartIndex = flightEndIndex * 0.25;
            double midEndIndex = flightEndIndex * 0.75;

            double bestDurationSeconds = double.NegativeInfinity;
            double bestMeanZ = 0.0;
            double bestStdZ = 0.0;

            int segmentCount = fullResult.Segments.Count;
            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                SegmentClassificationResult segmentResult = fullResult.Segments[segmentIndex];
                SegmentFeatures segmentFeatures = segmentResult.FeatureValues;

                if (IsValidCruiseStatsCandidate(segmentResult, segmentFeatures, midStartIndex, midEndIndex) == false)
                {
                    continue;
                }

                double durationSeconds = segmentFeatures.DurationSeconds;
                if (durationSeconds > bestDurationSeconds)
                {
                    bestDurationSeconds = durationSeconds;
                    bestMeanZ = segmentFeatures.MeanZ;
                    bestStdZ = segmentFeatures.StdZ;
                }
            }

            bool hasCruiseStats = bestDurationSeconds > 0.0;
            return hasCruiseStats ? (true, bestMeanZ, bestStdZ) : (false, 0.0, 0.0);

        }

        private bool IsValidCruiseStatsCandidate(
            SegmentClassificationResult segmentResult,
            SegmentFeatures segmentFeatures,
            double midStartIndex,
            double midEndIndex)
        {
            bool isStable = IsStableLabel(segmentResult.Label);

            int segmentStartIndex = segmentResult.Segment.StartIndex;
            int segmentEndIndex = segmentResult.Segment.EndIndex;

            bool isInsideWindow = segmentEndIndex >= midStartIndex && segmentStartIndex <= midEndIndex;

            bool isValid =
                isStable &&
                isInsideWindow;

            return isValid;
        }

        private double ComputeMedianAbsSlope(SegmentAnalysisResult fullResult)
        {
            List<double> absoluteSlopes = new List<double>();

            int segmentCount = fullResult.Segments.Count;
            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                SegmentFeatures segmentFeatures = fullResult.Segments[segmentIndex].FeatureValues;
                double slope = segmentFeatures.Slope;
                absoluteSlopes.Add(Math.Abs(slope));
            }

            absoluteSlopes.Sort();

            int slopeCount = absoluteSlopes.Count;
            if (slopeCount % 2 == 1)
            {
                return absoluteSlopes[slopeCount / 2];
            }

            double leftMiddle = absoluteSlopes[(slopeCount / 2) - 1];
            double rightMiddle = absoluteSlopes[slopeCount / 2];
            return (leftMiddle + rightMiddle) / 2.0;
        }
    }
}
