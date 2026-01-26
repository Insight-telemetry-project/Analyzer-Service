using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Enums;
using Analyzer_Service.Models.Interface.Algorithms.AnomalyDetector;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Services.Algorithms.Pelt;

namespace Analyzer_Service.Services.Algorithms.AnomalyDetector
{
    public class AnomalyDetectionUtility : IAnomalyDetectionUtility
    {
        private readonly IPatternHashingUtility patternHashingUtility;
        private readonly ITuningSettingsFactory tuningSettingsFactory;

        private readonly HashSet<SegmentLabel> nonAnomalousLabels = new HashSet<SegmentLabel>
        {
            SegmentLabel.PatternOK,
            SegmentLabel.Steady,
            SegmentLabel.Neutral,
        };

        public AnomalyDetectionUtility(
            IPatternHashingUtility patternHashingUtility,
            ITuningSettingsFactory tuningSettingsFactory)
        {
            this.patternHashingUtility = patternHashingUtility;
            this.tuningSettingsFactory = tuningSettingsFactory;
        }

        public List<int> DetectAnomalies(
            List<double> processedSignalValues,
            List<SegmentBoundary> segmentBoundaries,
            List<string> segmentLabelList,
            List<SegmentFeatures> featureValueList,
            flightStatus status)
        {
            int totalSegmentCount = segmentBoundaries.Count;

            Dictionary<string, int> labelOccurrences =
                segmentLabelList
                    .GroupBy(label => label)
                    .ToDictionary(group => group.Key, group => group.Count());

            double totalSegmentTime =
                CalculateTotalSegmentTime(segmentBoundaries, totalSegmentCount);

            Dictionary<string, double> labelDurationTotals =
                GetLabelDurationTotals(segmentBoundaries, segmentLabelList, totalSegmentCount);

            HashSet<SegmentLabel> rareSegmentLabels =
                GetRareLabels(labelOccurrences, labelDurationTotals, totalSegmentTime, status);

            int[] patternSupportArray =
                ComputePatternSupportArray(processedSignalValues, segmentBoundaries);

            List<int> candidateSegmentIndexes =
                GetCandidateSegmentIndexes(
                    segmentBoundaries,
                    segmentLabelList,
                    featureValueList,
                    rareSegmentLabels,
                    patternSupportArray,
                    status);

            return ApplyPostFiltering(segmentBoundaries, candidateSegmentIndexes, status);
        }

        private static double CalculateTotalSegmentTime(
            List<SegmentBoundary> segmentBoundaries,
            int totalSegmentCount)
        {
            SegmentBoundary firstSegment = segmentBoundaries[0];
            SegmentBoundary lastSegment = segmentBoundaries[totalSegmentCount - 1];

            double startTime = firstSegment.StartIndex;
            double endTime = lastSegment.EndIndex - 1;

            return endTime - startTime;
        }

        private static Dictionary<string, double> GetLabelDurationTotals(
            List<SegmentBoundary> segmentBoundaries,
            List<string> segmentLabelList,
            int totalSegmentCount)
        {
            Dictionary<string, double> durationTotals = new Dictionary<string, double>();

            for (int segmentIndex = 0; segmentIndex < totalSegmentCount; segmentIndex++)
            {
                SegmentBoundary segmentBoundary = segmentBoundaries[segmentIndex];

                double segmentDuration =
                    (segmentBoundary.EndIndex - 1) - segmentBoundary.StartIndex;

                string segmentLabelRaw = segmentLabelList[segmentIndex];

                if (!durationTotals.ContainsKey(segmentLabelRaw))
                {
                    durationTotals[segmentLabelRaw] = 0.0;
                }

                durationTotals[segmentLabelRaw] += segmentDuration;
            }

            return durationTotals;
        }

        private HashSet<SegmentLabel> GetRareLabels(
            Dictionary<string, int> labelOccurrences,
            Dictionary<string, double> labelDurationTotals,
            double totalSegmentTime,
            flightStatus status)
        {
            PeltTuningSettings settings = tuningSettingsFactory.Get(status);

            HashSet<SegmentLabel> rareLabels = new HashSet<SegmentLabel>();

            foreach (KeyValuePair<string, int> labelEntry in labelOccurrences)
            {
                bool parsed = Enum.TryParse(labelEntry.Key, out SegmentLabel segmentLabelEnum);
                if (!parsed)
                {
                    continue;
                }

                double labelTimeFraction =
                    labelDurationTotals.ContainsKey(labelEntry.Key)
                        ? labelDurationTotals[labelEntry.Key] / totalSegmentTime
                        : 0.0;

                bool isRareLabel =
                    !nonAnomalousLabels.Contains(segmentLabelEnum) &&
                    (labelEntry.Value <= settings.RARE_LABEL_COUNT_MAX ||
                     labelTimeFraction <= settings.RARE_LABEL_TIME_FRACTION);

                if (isRareLabel)
                {
                    rareLabels.Add(segmentLabelEnum);
                }
            }

            return rareLabels;
        }

        private int[] ComputePatternSupportArray(
            List<double> processedSignalValues,
            List<SegmentBoundary> segmentBoundaries)
        {
            int totalSegmentCount = segmentBoundaries.Count;

            List<string> patternHashList = new List<string>(totalSegmentCount);

            for (int segmentIndex = 0; segmentIndex < totalSegmentCount; segmentIndex++)
            {
                SegmentBoundary segmentBoundary = segmentBoundaries[segmentIndex];

                string segmentPatternHash =
                    patternHashingUtility.ComputeHash(processedSignalValues, segmentBoundary);

                patternHashList.Add(segmentPatternHash);
            }

            Dictionary<string, int> hashOccurrences =
                patternHashList
                    .GroupBy(hash => hash)
                    .ToDictionary(group => group.Key, group => group.Count());

            int[] patternSupportArray =
                patternHashList
                    .Select(hash => hashOccurrences[hash] - 1)
                    .ToArray();

            return patternSupportArray;
        }

        private List<int> GetCandidateSegmentIndexes(
            List<SegmentBoundary> segmentBoundaries,
            List<string> segmentLabelList,
            List<SegmentFeatures> featureValueList,
            HashSet<SegmentLabel> rareSegmentLabels,
            int[] patternSupportArray,
            flightStatus status)
        {
            PeltTuningSettings settings = tuningSettingsFactory.Get(status);

            List<int> candidateSegmentIndexList = new List<int>();
            int totalSegmentCount = segmentBoundaries.Count;

            for (int segmentIndex = 0; segmentIndex < totalSegmentCount; segmentIndex++)
            {
                Enum.TryParse(segmentLabelList[segmentIndex], out SegmentLabel segmentLabelEnum);

                SegmentFeatures featureValues = featureValueList[segmentIndex];

                double duration = featureValues.DurationSeconds;
                double rangeZ = featureValues.RangeZ;

                bool isDurationValid = duration >= settings.MINIMUM_DURATION_SECONDS;
                bool isRangeValid = rangeZ >= settings.MINIMUM_RANGEZ;
                bool isPatternSupportValid = patternSupportArray[segmentIndex] < settings.PATTERN_SUPPORT_THRESHOLD;

                bool isSegmentValid = isDurationValid && isRangeValid && isPatternSupportValid;

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
            List<SegmentBoundary> segmentBoundaries,
            List<int> candidateSegmentIndexes,
            flightStatus status)
        {
            PeltTuningSettings settings = tuningSettingsFactory.Get(status);

            List<SegmentMidpoint> segmentMidpointList = new List<SegmentMidpoint>();

            for (int listIndex = 0; listIndex < candidateSegmentIndexes.Count; listIndex++)
            {
                int segmentIndex = candidateSegmentIndexes[listIndex];
                SegmentBoundary segmentBoundary = segmentBoundaries[segmentIndex];

                double midpointTime =
                    0.5 * (segmentBoundary.StartIndex + (segmentBoundary.EndIndex - 1));

                SegmentMidpoint segmentMidpoint = new SegmentMidpoint
                {
                    SegmentIndex = segmentIndex,
                    MidTime = midpointTime
                };

                segmentMidpointList.Add(segmentMidpoint);
            }

            List<SegmentMidpoint> orderedMidpoints =
                segmentMidpointList
                    .OrderBy(item => item.MidTime)
                    .ToList();

            List<int> filteredSegmentIndexList = new List<int>();
            double lastAcceptedMidTime = ConstantAnomalyDetection.INITIAL_MIN_TIME;

            for (int midpointIndex = 0; midpointIndex < orderedMidpoints.Count; midpointIndex++)
            {
                SegmentMidpoint midpointInfo = orderedMidpoints[midpointIndex];

                bool isGapLargeEnough =
                    midpointInfo.MidTime - lastAcceptedMidTime >=
                    settings.POST_MINIMUM_GAP_SECONDS;

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
