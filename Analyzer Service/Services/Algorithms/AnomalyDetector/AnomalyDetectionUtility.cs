using System;
using System.Collections.Generic;
using System.Linq;
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
            SegmentLabel.Neutral
        };

        public AnomalyDetectionUtility(
            IPatternHashingUtility patternHashingUtility,
            ITuningSettingsFactory tuningSettingsFactory)
        {
            this.patternHashingUtility = patternHashingUtility;
            this.tuningSettingsFactory = tuningSettingsFactory;
        }

        public List<int> DetectAnomalies(
            double[] processedSignalValues,
            IReadOnlyList<SegmentBoundary> segmentBoundaries,
            IReadOnlyList<SegmentClassificationResult> classificationResults,
            IReadOnlyList<SegmentFeatures> featureValueList,
            flightStatus status)
        {
            int totalSegmentCount = segmentBoundaries.Count;

            Dictionary<string, int> labelOccurrences =
                BuildLabelOccurrences(classificationResults);


            double totalSegmentTime =
                CalculateTotalSegmentTime(segmentBoundaries, totalSegmentCount);

            Dictionary<string, double> labelDurationTotals =
                GetLabelDurationTotals(segmentBoundaries, classificationResults, totalSegmentCount);

            HashSet<SegmentLabel> rareSegmentLabels =
                GetRareLabels(labelOccurrences, labelDurationTotals, totalSegmentTime, status);

            int[] patternSupportArray =
                ComputePatternSupportArray(processedSignalValues, segmentBoundaries);

            List<int> candidateSegmentIndexes =
                GetCandidateSegmentIndexes(
                    segmentBoundaries,
                    classificationResults,
                    featureValueList,
                    rareSegmentLabels,
                    patternSupportArray,
                    status);

            return ApplyPostFiltering(segmentBoundaries, candidateSegmentIndexes, status);
        }

        private static Dictionary<string, int> BuildLabelOccurrences(
            IReadOnlyList<SegmentClassificationResult> classificationResults)
        {
            Dictionary<string, int> labelOccurrences = new Dictionary<string, int>();

            for (int segmentIndex = 0; segmentIndex < classificationResults.Count; segmentIndex++)
            {
                string label = classificationResults[segmentIndex].Label;

                if (!labelOccurrences.ContainsKey(label))
                {
                    labelOccurrences[label] = 0;
                }

                labelOccurrences[label]++;
            }

            return labelOccurrences;
        }


        private double CalculateTotalSegmentTime(
            IReadOnlyList<SegmentBoundary> segmentBoundaries,int totalSegmentCount)
        {
            int firstStartIndex = segmentBoundaries[0].StartIndex;
            int lastEndIndexExclusive = segmentBoundaries[totalSegmentCount - 1].EndIndex;

            double totalTime = (lastEndIndexExclusive - 1) - firstStartIndex;
            return totalTime;
        }


        private Dictionary<string, double> GetLabelDurationTotals(
            IReadOnlyList<SegmentBoundary> segmentBoundaries,
            IReadOnlyList<SegmentClassificationResult> classificationResults,
            int totalSegmentCount)

        {
            Dictionary<string, double> durationTotals = new Dictionary<string, double>();

            for (int segmentIndex = 0; segmentIndex < totalSegmentCount; segmentIndex++)
            {
                SegmentBoundary segmentBoundary = segmentBoundaries[segmentIndex];

                double segmentDurationSeconds =
                    (segmentBoundary.EndIndex - 1) - segmentBoundary.StartIndex;

                string label = classificationResults[segmentIndex].Label;

                if (!durationTotals.ContainsKey(label))
                {
                    durationTotals[label] = 0.0;
                }

                durationTotals[label] += segmentDurationSeconds;
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
                SegmentLabel segmentLabelEnum =
                    (SegmentLabel)Enum.Parse(typeof(SegmentLabel), labelEntry.Key);

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
            double[] processedSignalValues, IReadOnlyList<SegmentBoundary> segmentBoundaries)
        {
            int totalSegmentCount = segmentBoundaries.Count;

            List<string> patternHashList = new List<string>(totalSegmentCount);
            Dictionary<string, int> hashOccurrencesByHash = new Dictionary<string, int>();

            for (int segmentIndex = 0; segmentIndex < totalSegmentCount; segmentIndex++)
            {
                SegmentBoundary segmentBoundary = segmentBoundaries[segmentIndex];

                string patternHash =
                    patternHashingUtility.ComputeHash(processedSignalValues, segmentBoundary);

                patternHashList.Add(patternHash);

                if (hashOccurrencesByHash.ContainsKey(patternHash))
                {
                    hashOccurrencesByHash[patternHash] = hashOccurrencesByHash[patternHash] + 1;
                }
                else
                {
                    hashOccurrencesByHash[patternHash] = 1;
                }
            }

            int[] patternSupportArray = new int[totalSegmentCount];

            for (int segmentIndex = 0; segmentIndex < totalSegmentCount; segmentIndex++)
            {
                string patternHash = patternHashList[segmentIndex];
                int totalOccurrences = hashOccurrencesByHash[patternHash];

                patternSupportArray[segmentIndex] = totalOccurrences - 1;
            }

            return patternSupportArray;
        }


        private List<int> GetCandidateSegmentIndexes(
            IReadOnlyList<SegmentBoundary> segmentBoundaries,
            IReadOnlyList<SegmentClassificationResult> classificationResults,
            IReadOnlyList<SegmentFeatures> featureValueList,
            HashSet<SegmentLabel> rareSegmentLabels,
            int[] patternSupportArray,
            flightStatus status)
        {
            PeltTuningSettings settings = tuningSettingsFactory.Get(status);

            List<int> candidateSegmentIndexList = new List<int>();
            int totalSegmentCount = segmentBoundaries.Count;

            for (int segmentIndex = 0; segmentIndex < totalSegmentCount; segmentIndex++)
            {
                string label = classificationResults[segmentIndex].Label;

                SegmentLabel segmentLabelEnum =
                    (SegmentLabel)Enum.Parse(typeof(SegmentLabel), label);

                SegmentFeatures featureValues = featureValueList[segmentIndex];

                double durationSeconds = featureValues.DurationSeconds;
                double rangeZ = featureValues.RangeZ;

                bool isDurationValid = durationSeconds >= settings.MINIMUM_DURATION_SECONDS;
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
    IReadOnlyList<SegmentBoundary> segmentBoundaries,
    List<int> candidateSegmentIndexes,
    flightStatus status)
        {
            PeltTuningSettings settings = tuningSettingsFactory.Get(status);

            List<SegmentMidpoint> segmentMidpointList =
                new List<SegmentMidpoint>(candidateSegmentIndexes.Count);

            for (int candidateIndex = 0; candidateIndex < candidateSegmentIndexes.Count; candidateIndex++)
            {
                int segmentIndex = candidateSegmentIndexes[candidateIndex];
                SegmentBoundary segmentBoundary = segmentBoundaries[segmentIndex];

                double midpointTime = 0.5 * (segmentBoundary.StartIndex + segmentBoundary.EndIndex - 1);

                SegmentMidpoint segmentMidpoint = new SegmentMidpoint
                {
                    SegmentIndex = segmentIndex,
                    MidTime = midpointTime
                };

                segmentMidpointList.Add(segmentMidpoint);
            }

            segmentMidpointList.Sort(
                (firstMidpoint, secondMidpoint) => firstMidpoint.MidTime.CompareTo(secondMidpoint.MidTime));

            List<int> filteredSegmentIndexList = new List<int>(segmentMidpointList.Count);
            double lastAcceptedMidTime = ConstantAnomalyDetection.INITIAL_MIN_TIME;

            for (int midpointIndex = 0; midpointIndex < segmentMidpointList.Count; midpointIndex++)
            {
                SegmentMidpoint midpointInfo = segmentMidpointList[midpointIndex];

                bool isGapLargeEnough =
                    midpointInfo.MidTime - lastAcceptedMidTime >= settings.POST_MINIMUM_GAP_SECONDS;

                if (isGapLargeEnough)
                {
                    filteredSegmentIndexList.Add(midpointInfo.SegmentIndex);
                    lastAcceptedMidTime = midpointInfo.MidTime;
                }
            }

            filteredSegmentIndexList.Sort();

            List<int> uniqueSortedIndexes = new List<int>(filteredSegmentIndexList.Count);

            for (int filteredIndex = 0; filteredIndex < filteredSegmentIndexList.Count; filteredIndex++)
            {
                int currentIndex = filteredSegmentIndexList[filteredIndex];

                if (uniqueSortedIndexes.Count == 0)
                {
                    uniqueSortedIndexes.Add(currentIndex);
                    continue;
                }

                int lastAddedIndex = uniqueSortedIndexes[uniqueSortedIndexes.Count - 1];

                if (currentIndex != lastAddedIndex)
                {
                    uniqueSortedIndexes.Add(currentIndex);
                }
            }

            return uniqueSortedIndexes;
        }

    }
}
