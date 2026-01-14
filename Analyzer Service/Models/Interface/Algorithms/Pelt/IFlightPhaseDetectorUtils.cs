using Analyzer_Service.Models.Dto;

namespace Analyzer_Service.Models.Interface.Algorithms.Pelt
{
    public interface IFlightPhaseDetectorUtils
    {
        double ComputeMedianAbsSlope(SegmentAnalysisResult fullResult);

        bool IsValidCruiseStatsCandidate(
            SegmentClassificationResult segmentResult,
            double midStartIndex,
            double midEndIndex);

        CruiseStats ComputeCruiseStats(
            SegmentAnalysisResult fullResult,
            int flightEndIndex);

        bool IsStableLabel(string label);

        bool IsValidLandingStableCandidate(
            SegmentClassificationResult stableCandidate,
            SegmentFeatures stableFeatures,
            double stableAbsSlopeThreshold);
    }
}
