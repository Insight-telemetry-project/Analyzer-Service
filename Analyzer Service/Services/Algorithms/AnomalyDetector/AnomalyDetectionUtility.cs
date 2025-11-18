using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms.AnomalyDetector;

namespace Analyzer_Service.Services.Algorithms.AnomalyDetector
{
    public class AnomalyDetectionUtility : IAnomalyDetectionUtility
    {
        private readonly IPatternHashingUtility patternHashingUtility;

        private const double MinimumDurationSeconds = 0.6;
        private const double MinimumRangeZ = 0.5;
        private const int PatternSupportThreshold = 2;
        private const double RareLabelCountMax = 3.0;
        private const double RareLabelTimeFraction = 0.05;
        private const double PostMinimumGapSeconds = 8.0;
        private const double PostBinSeconds = 60.0;
        private const int PostMaximumPerBin = 1;

        public AnomalyDetectionUtility(IPatternHashingUtility patternHashingUtility)
        {
            this.patternHashingUtility = patternHashingUtility;
        }

        public List<int> DetectAnomalies(
            List<double> timeSeries,
            List<double> processedSignal,
            List<SegmentBoundary> segmentList,
            List<string> labelList,
            List<Dictionary<string, double>> featureList)
        {
            int segmentCount = segmentList.Count;
            if (segmentCount == 0)
            {
                return new List<int>();
            }

            Dictionary<string, int> labelCounts =
                labelList
                    .GroupBy(label => label)
                    .ToDictionary(group => group.Key, group => group.Count());

            double totalTime =
                timeSeries[segmentList[segmentCount - 1].EndIndex - 1] -
                timeSeries[segmentList[0].StartIndex];

            if (totalTime <= 0.0)
            {
                totalTime = 1.0;
            }

            Dictionary<string, double> labelDurations = new Dictionary<string, double>();

            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                SegmentBoundary segmentBoundary = segmentList[segmentIndex];

                double segmentDuration =
                    timeSeries[segmentBoundary.EndIndex - 1] -
                    timeSeries[segmentBoundary.StartIndex];

                string label = labelList[segmentIndex];

                if (!labelDurations.ContainsKey(label))
                {
                    labelDurations[label] = 0.0;
                }

                labelDurations[label] += segmentDuration;
            }

            HashSet<string> boringLabels = new HashSet<string>
            {
                "PatternOK",
                "Steady",
                "Neutral"
            };

            HashSet<string> rareLabels = new HashSet<string>();

            foreach (KeyValuePair<string, int> labelCountPair in labelCounts)
            {
                string label = labelCountPair.Key;
                int count = labelCountPair.Value;

                double fraction =
                    labelDurations.ContainsKey(label)
                        ? labelDurations[label] / totalTime
                        : 0.0;

                bool isRare =
                    !boringLabels.Contains(label) &&
                    (count <= RareLabelCountMax || fraction <= RareLabelTimeFraction);

                if (isRare)
                {
                    rareLabels.Add(label);
                }
            }

            List<string> patternHashes = new List<string>();

            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                SegmentBoundary segmentBoundary = segmentList[segmentIndex];

                string hash =
                    patternHashingUtility.ComputeHash(
                        timeSeries,
                        processedSignal,
                        segmentBoundary);

                patternHashes.Add(hash);
            }

            Dictionary<string, int> patternCounts =
                patternHashes
                    .GroupBy(hash => hash)
                    .ToDictionary(group => group.Key, group => group.Count());

            int[] patternSupportArray =
                patternHashes
                    .Select(hash => patternCounts[hash] - 1)
                    .ToArray();

            List<int> candidateSegmentIds = new List<int>();

            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                SegmentBoundary segmentBoundary = segmentList[segmentIndex];
                string label = labelList[segmentIndex];
                Dictionary<string, double> features = featureList[segmentIndex];

                double durationSeconds = features["duration_s"];
                double rangeZ = features["range_z"];

                if (durationSeconds < MinimumDurationSeconds)
                {
                    continue;
                }

                if (rangeZ < MinimumRangeZ)
                {
                    continue;
                }

                if (patternSupportArray[segmentIndex] >= PatternSupportThreshold)
                {
                    continue;
                }

                bool isRare = rareLabels.Contains(label);
                bool isNonBoring = !boringLabels.Contains(label);

                if (isRare || isNonBoring)
                {
                    candidateSegmentIds.Add(segmentIndex);
                }
            }

            List<int> finalSegmentIds =
                ApplyPostFiltering(timeSeries, segmentList, candidateSegmentIds);

            return finalSegmentIds;
        }

        private class SegmentMidpoint
        {
            public int SegmentIndex { get; set; }
            public double MidTime { get; set; }
        }

        private List<int> ApplyPostFiltering(
    List<double> timeSeries,
    List<SegmentBoundary> segmentList,
    List<int> candidateSegmentIds)
        {
            if (candidateSegmentIds.Count == 0)
            {
                return candidateSegmentIds;
            }

            List<SegmentMidpoint> midpointList = new List<SegmentMidpoint>();

            for (int index = 0; index < candidateSegmentIds.Count; index++)
            {
                int segmentIndex = candidateSegmentIds[index];
                SegmentBoundary segmentBoundary = segmentList[segmentIndex];

                double midpointTime =
                    0.5 * (timeSeries[segmentBoundary.StartIndex] +
                           timeSeries[segmentBoundary.EndIndex - 1]);

                SegmentMidpoint segmentMidpoint = new SegmentMidpoint
                {
                    SegmentIndex = segmentIndex,
                    MidTime = midpointTime
                };

                midpointList.Add(segmentMidpoint);
            }

            List<SegmentMidpoint> orderedMidpoints =
                midpointList
                    .OrderBy(item => item.MidTime)
                    .ToList();

            List<int> timeFilteredSegmentIds = new List<int>();
            double lastAcceptedTime = -1e18;

            for (int index = 0; index < orderedMidpoints.Count; index++)
            {
                SegmentMidpoint segmentMidpoint = orderedMidpoints[index];

                if (segmentMidpoint.MidTime - lastAcceptedTime >= PostMinimumGapSeconds)
                {
                    timeFilteredSegmentIds.Add(segmentMidpoint.SegmentIndex);
                    lastAcceptedTime = segmentMidpoint.MidTime;
                }
            }

            List<int> finalSegmentIds =
                timeFilteredSegmentIds
                    .Distinct()
                    .OrderBy(id => id)
                    .ToList();

            return finalSegmentIds;
        }

    }
}
