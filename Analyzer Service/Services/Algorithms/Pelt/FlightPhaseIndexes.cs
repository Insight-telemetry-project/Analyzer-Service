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

            double baselineMeanZ = fullResult.Segments[ConstantPelt.FirstSegmentIndex].FeatureValues.MeanZ;

            double medianAbsSlope = _flightPhaseDetectorUtils.ComputeMedianAbsSlope(fullResult);

            double stableAbsSlopeThreshold = Math.Max(medianAbsSlope * ConstantPelt.StableAbsSlopeMultiplier, 1e-9);
            double climbSlopeThreshold = Math.Max(medianAbsSlope * ConstantPelt.ClimbSlopeMultiplier, stableAbsSlopeThreshold * 2.0);
            double descentSlopeThreshold = Math.Max(medianAbsSlope * ConstantPelt.DescentSlopeMultiplier, stableAbsSlopeThreshold * 2.0);

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
            int maxTakeoffIndex = (int)Math.Round(flightEndIndex * ConstantPelt.TakeoffMustBeBeforePercent);

            double safeCruiseStdZ = Math.Max(cruiseStdZ, 1e-9);

            double requiredLevelByRise =
                baselineMeanZ + (ConstantPelt.TakeoffRiseFraction * (cruiseMeanZ - baselineMeanZ));

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
                durationSeconds >= ConstantPelt.TakeoffStableMinSeconds &&
                absSlope <= stableAbsSlopeThreshold &&
                distanceToCruiseStd <= ConstantPelt.TakeoffCruiseStdTolerance &&
                candidateMeanZ >= requiredLevelByRise;
        }

        private int DetectLandingStartIndex(
            SegmentAnalysisResult fullResult, int flightEndIndex, double stableAbsSlopeThreshold,
            double descentSlopeThreshold, double cruiseMeanZ, double cruiseStdZ, int takeoffEndIndex)
        {
            int landingSearchStartIndex = (int)Math.Round(flightEndIndex * ConstantPelt.LandingMustBeAfterPercent);

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

                double safeCruiseStdZ = Math.Max(cruiseStdZ, 1e-9);
                bool isMeaningfulDropByLevel = nextFeatures.MeanZ <= (cruiseMeanZ - (ConstantPelt.LandingDropStd * safeCruiseStdZ));

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
