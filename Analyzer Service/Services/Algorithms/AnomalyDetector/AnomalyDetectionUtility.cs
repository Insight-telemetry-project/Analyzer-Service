using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms.AnomalyDetector;
using Analyzer_Service.Models.Enums;

namespace Analyzer_Service.Services.Algorithms.AnomalyDetector
{
    public class AnomalyDetectionUtility : IAnomalyDetectionUtility
    {
        private readonly IPatternHashingUtility patternHashingUtility;

        private readonly HashSet<SegmentLabel> nonAnomalousLabels = new HashSet<SegmentLabel>
        {
            SegmentLabel.PatternOK,
            SegmentLabel.Steady,
            SegmentLabel.Neutral,


            //SegmentLabel.RampUp,
            //SegmentLabel.RampDown,
            //SegmentLabel.Oscillation



        };

        public AnomalyDetectionUtility(IPatternHashingUtility patternHashingUtility)
        {
            this.patternHashingUtility = patternHashingUtility;
        }

        public List<int> DetectAnomalies(
            List<double> timeSeries,
            List<double> processedSignal,
            List<SegmentBoundary> segmentBoundaries,
            List<string> segmentLabelList,
            List<Dictionary<string, double>> featureValueList)
        {
            int totalSegmentCount = segmentBoundaries.Count;

            Dictionary<string, int> labelOccurrences =
                segmentLabelList
                    .GroupBy(label => label)
                    .ToDictionary(group => group.Key, group => group.Count());

            double totalSegmentTime =
                CalculateTotalSegmentTime(timeSeries, segmentBoundaries, totalSegmentCount);

            Dictionary<string, double> labelDurationTotals =
                GetLabelDurationTotals(timeSeries, segmentBoundaries, segmentLabelList, totalSegmentCount);

            HashSet<SegmentLabel> rareSegmentLabels =
                GetRareLabels(labelOccurrences, labelDurationTotals, totalSegmentTime);

            int[] patternSupportArray =
                ComputePatternSupportArray(timeSeries, processedSignal, segmentBoundaries);

            List<int> candidateSegmentIndexes =
                GetCandidateSegmentIndexes(
                    segmentBoundaries,
                    segmentLabelList,
                    featureValueList,
                    rareSegmentLabels,
                    patternSupportArray);

            return ApplyPostFiltering(timeSeries, segmentBoundaries, candidateSegmentIndexes);
        }

        private double CalculateTotalSegmentTime(
            List<double> timeSeries,
            List<SegmentBoundary> segmentBoundaries,
            int totalSegmentCount)
        {
            double totalTime =
                timeSeries[segmentBoundaries[totalSegmentCount - 1].EndIndex - 1] -
                timeSeries[segmentBoundaries[0].StartIndex];
            return totalTime;
        }

        private Dictionary<string, double> GetLabelDurationTotals(
            List<double> timeSeries,
            List<SegmentBoundary> segmentBoundaries,
            List<string> segmentLabelList,
            int totalSegmentCount)
        {
            Dictionary<string, double> durationTotals = new Dictionary<string, double>();

            for (int segmentIndex = 0; segmentIndex < totalSegmentCount; segmentIndex++)
            {
                SegmentBoundary segmentBoundary = segmentBoundaries[segmentIndex];

                double segmentDurationSeconds =
                    timeSeries[segmentBoundary.EndIndex - 1] -
                    timeSeries[segmentBoundary.StartIndex];

                string segmentLabelRaw = segmentLabelList[segmentIndex];

                if (!durationTotals.ContainsKey(segmentLabelRaw))
                {
                    durationTotals[segmentLabelRaw] = 0.0;
                }

                durationTotals[segmentLabelRaw] += segmentDurationSeconds;
            }

            return durationTotals;
        }

        private HashSet<SegmentLabel> GetRareLabels(
            Dictionary<string, int> labelOccurrences,
            Dictionary<string, double> labelDurationTotals,
            double totalSegmentTime)
        {
            HashSet<SegmentLabel> rareLabels = new HashSet<SegmentLabel>();

            foreach (KeyValuePair<string, int> labelEntry in labelOccurrences)
            {
                if (Enum.TryParse(labelEntry.Key, out SegmentLabel segmentLabelEnum))
                {
                    double labelTimeFraction =
                        labelDurationTotals.ContainsKey(labelEntry.Key)
                            ? labelDurationTotals[labelEntry.Key] / totalSegmentTime
                            : 0.0;

                    bool isRareLabel =
                        !nonAnomalousLabels.Contains(segmentLabelEnum) &&
                        (labelEntry.Value <= ConstantAnomalyDetection.RARE_LABEL_COUNT_MAX ||
                         labelTimeFraction <= ConstantAnomalyDetection.RARE_LABEL_TIME_FRACTION);

                    if (isRareLabel)
                    {
                        rareLabels.Add(segmentLabelEnum);
                    }
                }
            }

            return rareLabels;
        }

        private int[] ComputePatternSupportArray(
            List<double> timeSeries,
            List<double> processedSignal,
            List<SegmentBoundary> segmentBoundaries)
        {
            int totalSegmentCount = segmentBoundaries.Count;

            List<string> patternHashList = new List<string>();

            for (int segmentIndex = 0; segmentIndex < totalSegmentCount; segmentIndex++)
            {
                string segmentPatternHash =
                    patternHashingUtility.ComputeHash(
                        timeSeries,
                        processedSignal,
                        segmentBoundaries[segmentIndex]);

                patternHashList.Add(segmentPatternHash);
            }

            Dictionary<string, int> hashOccurrences =
                patternHashList
                    .GroupBy(hash => hash)
                    .ToDictionary(group => group.Key, group => group.Count());

            int[] patternSupportArray =
                patternHashList.Select(hash => hashOccurrences[hash] - 1).ToArray();

            return patternSupportArray;
        }

        private List<int> GetCandidateSegmentIndexes(
            List<SegmentBoundary> segmentBoundaries,
            List<string> segmentLabelList,
            List<Dictionary<string, double>> featureValueList,
            HashSet<SegmentLabel> rareSegmentLabels,
            int[] patternSupportArray)
        {
            List<int> candidateSegmentIndexList = new List<int>();
            int totalSegmentCount = segmentBoundaries.Count;

            for (int segmentIndex = 0; segmentIndex < totalSegmentCount; segmentIndex++)
            {
                Enum.TryParse(segmentLabelList[segmentIndex], out SegmentLabel segmentLabelEnum);

                Dictionary<string, double> featureValues = featureValueList[segmentIndex];

                double durationSeconds =
                    featureValues[ConstantRandomForest.DURATION_S_JSON];

                double rangeZ =
                    featureValues[ConstantRandomForest.RANGE_Z_JSON];

                bool isDurationValid =
                    durationSeconds >= ConstantAnomalyDetection.MINIMUM_DURATION_SECONDS;

                bool isRangeValid =
                    rangeZ >= ConstantAnomalyDetection.MINIMUM_RANGEZ;

                bool isPatternSupportValid =
                    patternSupportArray[segmentIndex] <
                    ConstantAnomalyDetection.PATTERN_SUPPORT_THRESHOLD;

                bool isSegmentValid =
                    isDurationValid &&
                    isRangeValid &&
                    isPatternSupportValid;

                if (isSegmentValid)
                {
                    bool isRareLabel = rareSegmentLabels.Contains(segmentLabelEnum);
                    bool isNonStableLabel = !nonAnomalousLabels.Contains(segmentLabelEnum);

                    if (isRareLabel || isNonStableLabel)
                    {
                        candidateSegmentIndexList.Add(segmentIndex);
                    }
                }
            }

            return candidateSegmentIndexList;
        }

        private List<int> ApplyPostFiltering(
            List<double> timeSeries,
            List<SegmentBoundary> segmentBoundaries,
            List<int> candidateSegmentIndexes)
        {
            List<SegmentMidpoint> segmentMidpointList = new List<SegmentMidpoint>();

            for (int listIndex = 0; listIndex < candidateSegmentIndexes.Count; listIndex++)
            {
                int segmentIndex = candidateSegmentIndexes[listIndex];
                SegmentBoundary segmentBoundary = segmentBoundaries[segmentIndex];

                double midpointTime =
                    0.5 *
                    (timeSeries[segmentBoundary.StartIndex] +
                     timeSeries[segmentBoundary.EndIndex - 1]);

                segmentMidpointList.Add(new SegmentMidpoint
                {
                    SegmentIndex = segmentIndex,
                    MidTime = midpointTime
                });
            }

            List<SegmentMidpoint> orderedMidpoints =
                segmentMidpointList.OrderBy(item => item.MidTime).ToList();

            List<int> filteredSegmentIndexList = new List<int>();
            double lastAcceptedMidTime = ConstantAnomalyDetection.INITIAL_MIN_TIME;

            for (int midpointIndex = 0; midpointIndex < orderedMidpoints.Count; midpointIndex++)
            {
                SegmentMidpoint midpointInfo = orderedMidpoints[midpointIndex];

                bool isGapLargeEnough =
                    midpointInfo.MidTime - lastAcceptedMidTime >=
                    ConstantAnomalyDetection.POST_MINIMUM_GAP_SECONDS;

                if (isGapLargeEnough)
                {
                    filteredSegmentIndexList.Add(midpointInfo.SegmentIndex);
                    lastAcceptedMidTime = midpointInfo.MidTime;
                }
            }

            return filteredSegmentIndexList
                .Distinct()
                .OrderBy(index => index)
                .ToList();
        }
    }
}
