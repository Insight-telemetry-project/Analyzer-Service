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

            CruiseStats cruiseStatsResult = ComputeCruiseStats(fullResult, flightEndIndex);

            double baselineMeanZ = fullResult.Segments[0].FeatureValues.MeanZ;

            double medianAbsSlope = ComputeMedianAbsSlope(fullResult);

            double stableAbsSlopeThreshold = Math.Max(medianAbsSlope * StableAbsSlopeMultiplier, 1e-9);
            double climbSlopeThreshold = Math.Max(medianAbsSlope * ClimbSlopeMultiplier, stableAbsSlopeThreshold * 2.0);
            double descentSlopeThreshold = Math.Max(medianAbsSlope * DescentSlopeMultiplier, stableAbsSlopeThreshold * 2.0);

            int takeoffEndIndex = DetectTakeoffEndIndex(
                fullResult,
                flightEndIndex,
                stableAbsSlopeThreshold,
                climbSlopeThreshold,
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

            return new FlightPhaseIndexes(takeoffEndIndex, landingStartIndex);
        }

        private int DetectTakeoffEndIndex(
            SegmentAnalysisResult fullResult,
            int flightEndIndex,
            double stableAbsSlopeThreshold,
            double climbSlopeThreshold,
            double cruiseMeanZ,
            double cruiseStdZ,
            double baselineMeanZ)
        {
            int maxTakeoffIndex = (int)Math.Round(flightEndIndex * TakeoffMustBeBeforePercent);

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

                if (segmentFeatures.Slope >= climbSlopeThreshold)
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
                        requiredLevelByRise))
                {
                    return Math.Max(0, segmentResult.Segment.StartIndex);
                }
            }

            return 0;
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

            return
                sawRealClimb &&
                isStable &&
                durationSeconds >= TakeoffStableMinSeconds &&
                absSlope <= stableAbsSlopeThreshold &&
                distanceToCruiseStd <= TakeoffCruiseStdTolerance &&
                candidateMeanZ >= requiredLevelByRise;
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

                if (!IsValidLandingStableCandidate(stableCandidate, stableFeatures, stableAbsSlopeThreshold))
                {
                    continue;
                }

                int nextSegmentIndex = segmentIndex + 1;
                if (nextSegmentIndex >= segmentCount)
                {
                    return stableEndIndex;
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

                if (isRampDown || isStrongDescentBySlope || isMeaningfulDropByLevel)
                {
                    return nextSegment.Segment.StartIndex;
                }

                return stableEndIndex;
            }

            return takeoffEndIndex;
        }

        private bool IsValidLandingStableCandidate(
            SegmentClassificationResult stableCandidate,
            SegmentFeatures stableFeatures,
            double stableAbsSlopeThreshold)
        {
            return
                IsStableLabel(stableCandidate.Label) &&
                stableFeatures.DurationSeconds >= LandingStableMinSeconds &&
                Math.Abs(stableFeatures.Slope) <= stableAbsSlopeThreshold;
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

        private CruiseStats ComputeCruiseStats(
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

                if (!IsValidCruiseStatsCandidate(segmentResult, segmentFeatures, midStartIndex, midEndIndex))
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
            return new CruiseStats(true, bestMeanZ, bestStdZ);
        }

        private bool IsValidCruiseStatsCandidate(
            SegmentClassificationResult segmentResult,
            SegmentFeatures segmentFeatures,
            double midStartIndex,
            double midEndIndex)
        {
            int segmentStartIndex = segmentResult.Segment.StartIndex;
            int segmentEndIndex = segmentResult.Segment.EndIndex;

            return
                IsStableLabel(segmentResult.Label) &&
                segmentEndIndex >= midStartIndex &&
                segmentStartIndex <= midEndIndex;
        }



        private double ComputeMedianAbsSlope(SegmentAnalysisResult fullResult)
        {
            List<double> absoluteSlopes = new List<double>();

            int segmentCount = fullResult.Segments.Count;
            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                absoluteSlopes.Add(Math.Abs(fullResult.Segments[segmentIndex].FeatureValues.Slope));
            }

            absoluteSlopes.Sort();

            int slopeCount = absoluteSlopes.Count;
            if (slopeCount % 2 == 1)
            {
                return absoluteSlopes[slopeCount / 2];
            }

            return (absoluteSlopes[(slopeCount / 2) - 1] + absoluteSlopes[slopeCount / 2]) / 2.0;
        }
    }
}
