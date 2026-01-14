using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using System.Runtime.CompilerServices;

namespace Analyzer_Service.Services.Algorithms.Pelt
{
    public static class FlightPhaseDetectorExtensions
    {
        public static double ComputeMedianAbsSlope(this FlightPhaseDetector flightPhase ,SegmentAnalysisResult fullResult)
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


        public static bool IsValidCruiseStatsCandidate(
            this FlightPhaseDetector flightPhase,
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

        public static CruiseStats ComputeCruiseStats(
            this FlightPhaseDetector flightPhase,
            SegmentAnalysisResult fullResult,
            int flightEndIndex)
        {
            double midStartIndex = flightEndIndex * ConstantPelt.TAKE_OF_AREA;
            double midEndIndex = flightEndIndex * ConstantPelt.LANDING_AREA;

            double bestDurationSeconds = double.NegativeInfinity;
            double bestMeanZ = 0.0;
            double bestStdZ = 0.0;

            int segmentCount = fullResult.Segments.Count;
            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                SegmentClassificationResult segmentResult = fullResult.Segments[segmentIndex];
                SegmentFeatures segmentFeatures = segmentResult.FeatureValues;

                if (!flightPhase.IsValidCruiseStatsCandidate(segmentResult, segmentFeatures, midStartIndex, midEndIndex))
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


        public static bool IsStableLabel(this string label)
        {
            return
                string.Equals(label, ConstantRandomForest.STEADY, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(label, ConstantRandomForest.NEUTRAL, StringComparison.OrdinalIgnoreCase);
        }



        public static bool IsValidLandingStableCandidate(
            this FlightPhaseDetector flightPhase,
            SegmentClassificationResult stableCandidate,
            SegmentFeatures stableFeatures,
            double stableAbsSlopeThreshold)
        {
            return
                IsStableLabel(stableCandidate.Label) &&
                stableFeatures.DurationSeconds >= ConstantPelt.LandingStableMinSeconds &&
                Math.Abs(stableFeatures.Slope) <= stableAbsSlopeThreshold;
        }
    }
}
