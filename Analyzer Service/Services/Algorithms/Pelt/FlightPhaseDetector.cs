using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using System;

namespace Analyzer_Service.Services.Algorithms.Pelt
{
    public class FlightPhaseDetector : IFlightPhaseDetector
    {
        private readonly IFlightPhaseDetectorUtils _flightPhaseDetectorUtils;
        public FlightPhaseDetector(IFlightPhaseDetectorUtils flightPhaseDetectorUtils) 
        {
            _flightPhaseDetectorUtils = flightPhaseDetectorUtils;
        }
        public FlightPhaseIndexes Detect(SegmentAnalysisResult fullResult)
        {
            int flightEndIndex = fullResult.GetFlightEndIndex(fullResult.Segments.Count);

            CruiseStats cruiseStatsResult = _flightPhaseDetectorUtils.ComputeCruiseStats(fullResult, flightEndIndex);

            double baselineMeanZ = fullResult.Segments[ConstantPelt.FIRST_SEGMENTINDEX].FeatureValues.MeanZ;

            double medianAbsSlope = _flightPhaseDetectorUtils.ComputeMedianAbsSlope(fullResult);

            double stableAbsSlopeThreshold = Math.Max(medianAbsSlope * ConstantPelt.STABLE_ABS_SLOPE_MULTIPLIER, ConstantAlgorithm.NOT_DIVIDE_IN_ZERO);
            double climbSlopeThreshold = Math.Max(medianAbsSlope * ConstantPelt.CLIMB_SLOPE_MULTIPLIER, stableAbsSlopeThreshold * ConstantPelt.MULTIPLIER_THRESHOLD);
            double descentSlopeThreshold = Math.Max(medianAbsSlope * ConstantPelt.DESCENT_SLOPE_MULTIPLIER, stableAbsSlopeThreshold * ConstantPelt.MULTIPLIER_THRESHOLD);

            int takeoffEndIndex = DetectTakeoffEndIndex(
                fullResult,
                flightEndIndex,
                stableAbsSlopeThreshold,
                climbSlopeThreshold,
                cruiseStatsResult.cruiseMeanZ,
                cruiseStatsResult.cruiseStdZ,
                baselineMeanZ);

            int landingStartIndex = DetectLandingStartIndex( fullResult, flightEndIndex, stableAbsSlopeThreshold,
                                                           descentSlopeThreshold, cruiseStatsResult.cruiseMeanZ, cruiseStatsResult.cruiseStdZ,
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
            int maxTakeoffIndex = (int)Math.Round(flightEndIndex * ConstantPelt.Takeoff_Must_Be_Before_Percent);

            double safeCruiseStdZ = Math.Max(cruiseStdZ, 1e-9);

            double requiredLevelByRise =
                baselineMeanZ + (ConstantPelt.TAKEOFF_RISE_FRACTION * (cruiseMeanZ - baselineMeanZ));

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
            bool isStable = _flightPhaseDetectorUtils.IsStableLabel(segmentResult.Label);

            double durationSeconds = segmentFeatures.DurationSeconds;
            double absSlope = Math.Abs(segmentFeatures.Slope);
            double candidateMeanZ = segmentFeatures.MeanZ;

            double distanceToCruiseStd = Math.Abs(candidateMeanZ - cruiseMeanZ) / safeCruiseStdZ;

            return
                isStable &&
                durationSeconds >= ConstantPelt.TAKEOFF_STABLE_MIN_SECONDS &&
                absSlope <= stableAbsSlopeThreshold &&
                distanceToCruiseStd <= ConstantPelt.TAKEOFF_CRUISE_STD_TO_LERANCE &&
                candidateMeanZ >= requiredLevelByRise;
        }

        private int DetectLandingStartIndex(
            SegmentAnalysisResult fullResult, int flightEndIndex, double stableAbsSlopeThreshold,
            double descentSlopeThreshold, double cruiseMeanZ, double cruiseStdZ, int takeoffEndIndex)
        {
            int landingSearchStartIndex = (int)Math.Round(flightEndIndex * ConstantPelt.LANDING_MUST_BE_AFTER_PERCENT);

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

                if (!_flightPhaseDetectorUtils.IsValidLandingStableCandidate(stableCandidate, stableFeatures, stableAbsSlopeThreshold))
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

                double safeCruiseStdZ = Math.Max(cruiseStdZ, ConstantAlgorithm.NOT_DIVIDE_IN_ZERO);
                bool isMeaningfulDropByLevel = nextFeatures.MeanZ <= (cruiseMeanZ - (ConstantPelt.LANDING_DROP_STD * safeCruiseStdZ));

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
