using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using System;

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
        private const double LandingDropStd = 1.0;

        private const double TakeoffCruiseStdTolerance = 1.0;
        private const double TakeoffRiseFraction = 0.75;

        private const int FirstSegmentIndex = 0;

        public FlightPhaseIndexes Detect(SegmentAnalysisResult fullResult)
        {
            int flightEndIndex = fullResult.Segments[fullResult.Segments.Count - 1].Segment.EndIndex;

            CruiseStats cruiseStatsResult = this.ComputeCruiseStats(fullResult, flightEndIndex);

            double baselineMeanZ = fullResult.Segments[FirstSegmentIndex].FeatureValues.MeanZ;

            double medianAbsSlope = this.ComputeMedianAbsSlope(fullResult);

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

                

                if (IsValidTakeoffEndCandidate(
                        segmentResult,
                        segmentFeatures,
                        stableAbsSlopeThreshold,
                        cruiseMeanZ,
                        safeCruiseStdZ,
                        requiredLevelByRise))
                {
                    return Math.Max(0, segmentStartIndex);
                }
            }

            return 0;
        }

        private bool IsValidTakeoffEndCandidate(
            SegmentClassificationResult segmentResult,
            SegmentFeatures segmentFeatures,
            double stableAbsSlopeThreshold,
            double cruiseMeanZ,
            double safeCruiseStdZ,
            double requiredLevelByRise)
        {
            bool isStable = segmentResult.Label.IsStableLabel();

            double durationSeconds = segmentFeatures.DurationSeconds;
            double absSlope = Math.Abs(segmentFeatures.Slope);
            double candidateMeanZ = segmentFeatures.MeanZ;

            double distanceToCruiseStd = Math.Abs(candidateMeanZ - cruiseMeanZ) / safeCruiseStdZ;

            return
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

                if (!this.IsValidLandingStableCandidate(stableCandidate, stableFeatures, stableAbsSlopeThreshold))
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

                double safeCruiseStdZ = Math.Max(cruiseStdZ, 1e-9);
                bool isMeaningfulDropByLevel = nextFeatures.MeanZ <= (cruiseMeanZ - (LandingDropStd * safeCruiseStdZ));

                if (isRampDown || isStrongDescentBySlope || isMeaningfulDropByLevel)
                {
                    return nextSegment.Segment.StartIndex;
                }

                return stableEndIndex;
            }

            return takeoffEndIndex;
        }
    }
}
