using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using System;
using System.Collections.Generic;

namespace Analyzer_Service.Services.Algorithms.Pelt
{
    public class FlightPhaseDetectorUtils : IFlightPhaseDetectorUtils
    {
        public double ComputeMedianAbsSlope(SegmentAnalysisResult fullResult)
        {
            List<double> absoluteSlopes = new List<double>();

            int segmentCount = fullResult.Segments.Count;
            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                absoluteSlopes.Add(Math.Abs(fullResult.Segments[segmentIndex].FeatureValues.Slope));
            }

            absoluteSlopes.Sort();

            int slopeCount = absoluteSlopes.Count;
            if (slopeCount == 0)
            {
                return 0.0;
            }

            if (slopeCount % 2 == 1)
            {
                return absoluteSlopes[slopeCount / 2];
            }

            return (absoluteSlopes[(slopeCount / 2) - 1] + absoluteSlopes[slopeCount / 2]) / 2.0;
        }

        public bool IsValidCruiseStatsCandidate(
            SegmentClassificationResult segmentResult,
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

        public CruiseStats ComputeCruiseStats(
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

                if (!IsValidCruiseStatsCandidate(segmentResult, midStartIndex, midEndIndex))
                {
                    continue;
                }

                SegmentFeatures segmentFeatures = segmentResult.FeatureValues;
                double durationSeconds = segmentFeatures.DurationSeconds;

                if (durationSeconds > bestDurationSeconds)
                {
                    bestDurationSeconds = durationSeconds;
                    bestMeanZ = segmentFeatures.MeanZ;
                    bestStdZ = segmentFeatures.StdZ;
                }
            }

            return new CruiseStats(bestMeanZ, bestStdZ);
        }

        public bool IsStableLabel(string label)
        {
            return
                string.Equals(label, ConstantRandomForest.STEADY, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(label, ConstantRandomForest.NEUTRAL, StringComparison.OrdinalIgnoreCase);
        }

        public bool IsValidLandingStableCandidate(
            SegmentClassificationResult stableCandidate,
            SegmentFeatures stableFeatures,
            double stableAbsSlopeThreshold)
        {
            return
                IsStableLabel(stableCandidate.Label) &&
                stableFeatures.DurationSeconds >= ConstantPelt.LANDING_STABLE_MIN_SECONDS &&
                Math.Abs(stableFeatures.Slope) <= stableAbsSlopeThreshold;
        }
    }
}
