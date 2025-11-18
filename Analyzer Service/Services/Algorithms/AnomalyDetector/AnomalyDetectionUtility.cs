using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms.AnomalyDetector;
using Analyzer_Service.Models.Enums;

namespace Analyzer_Service.Services.Algorithms.AnomalyDetector
{
    public class AnomalyDetectionUtility : IAnomalyDetectionUtility
    {
        private readonly IPatternHashingUtility patternHashingUtility;


        private readonly HashSet<SegmentLabel> boringLabels = new HashSet<SegmentLabel>
        {
            SegmentLabel.PatternOK,
            SegmentLabel.Steady,
            SegmentLabel.Neutral
        };

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

            Dictionary<string, int> labelCounts =
                labelList
                    .GroupBy(label => label)
                    .ToDictionary(g => g.Key, g => g.Count());

            double totalTime =
                timeSeries[segmentList[segmentCount - 1].EndIndex - 1] -
                timeSeries[segmentList[0].StartIndex];

            if (totalTime <= 0.0)
                totalTime = 1.0;

            Dictionary<string, double> labelDurations = new Dictionary<string, double>();

            for (int index = 0; index < segmentCount; index++)
            {
                SegmentBoundary seg = segmentList[index];

                double duration =
                    timeSeries[seg.EndIndex - 1] -
                    timeSeries[seg.StartIndex];

                string labelRaw = labelList[index];

                if (!labelDurations.ContainsKey(labelRaw))
                    labelDurations[labelRaw] = 0.0;

                labelDurations[labelRaw] += duration;
            }

            HashSet<SegmentLabel> rareLabels = new HashSet<SegmentLabel>();

            foreach (KeyValuePair<string,int> pair in labelCounts)
            {
                if (!Enum.TryParse(pair.Key, out SegmentLabel lbl))
                    continue;

                double fraction =
                    labelDurations.ContainsKey(pair.Key)
                        ? labelDurations[pair.Key] / totalTime
                        : 0.0;

                bool isRare =
                    !boringLabels.Contains(lbl) &&
                    (pair.Value <= ConstantAnomalyDetection.RARE_LABEL_COUNT_MAX || fraction <= ConstantAnomalyDetection.RARE_LABEL_TIME_FRACTION);

                if (isRare)
                    rareLabels.Add(lbl);
            }

            List<string> patternHashes = new List<string>();

            for (int index = 0; index < segmentCount; index++)
            {
                SegmentBoundary seg = segmentList[index];

                string hash = patternHashingUtility.ComputeHash(
                    timeSeries,
                    processedSignal,
                    seg);

                patternHashes.Add(hash);
            }

            Dictionary<string, int> patternCounts =
                patternHashes
                    .GroupBy(h => h)
                    .ToDictionary(g => g.Key, g => g.Count());

            int[] patternSupportArray =
                patternHashes
                    .Select(h => patternCounts[h] - 1)
                    .ToArray();

            List<int> candidateSegmentIds = new List<int>();

            for (int index = 0; index < segmentCount; index++)
            {
                SegmentBoundary seg = segmentList[index];
                string rawLabel = labelList[index];

                Enum.TryParse(rawLabel, out SegmentLabel segLabel);

                Dictionary<string, double> features = featureList[index];

                double durationSeconds = features[ConstantRandomForest.DURATION_S_JSON];
                double rangeZ = features[ConstantRandomForest.RANGE_Z_JSON];

                if (durationSeconds < ConstantAnomalyDetection.MINIMUM_DURATION_SECONDS)
                    continue;

                if (rangeZ < ConstantAnomalyDetection.MINIMUM_RANGEZ)
                    continue;

                if (patternSupportArray[index] >= ConstantAnomalyDetection.PATTERN_SUPPORT_THRESHOLD)
                    continue;

                bool isRare = rareLabels.Contains(segLabel);
                bool isNonBoring = !boringLabels.Contains(segLabel);

                if (isRare || isNonBoring)
                    candidateSegmentIds.Add(index);
            }

            List<int> finalIds =
                ApplyPostFiltering(timeSeries, segmentList, candidateSegmentIds);

            return finalIds;
        }

        private List<int> ApplyPostFiltering(
            List<double> timeSeries,
            List<SegmentBoundary> segmentList,
            List<int> candidateSegmentIds)
        {

            List<SegmentMidpoint> midpoints = new List<SegmentMidpoint>();

            for (int index = 0; index < candidateSegmentIds.Count; index++)
            {
                int segIndex = candidateSegmentIds[index];
                SegmentBoundary seg = segmentList[segIndex];

                double mid =
                    0.5 * (timeSeries[seg.StartIndex] +
                           timeSeries[seg.EndIndex - 1]);

                midpoints.Add(new SegmentMidpoint
                {
                    SegmentIndex = segIndex,
                    MidTime = mid
                });
            }

            List<SegmentMidpoint> ordered =
                midpoints.OrderBy(m => m.MidTime).ToList();

            List<int> filtered = new List<int>();
            double lastTime = -1e18;

            for (int index = 0; index < ordered.Count; index++)
            {
                SegmentMidpoint mp = ordered[index];

                if (mp.MidTime - lastTime >= ConstantAnomalyDetection.POST_MINIMUM_GAP_SECONDS)
                {
                    filtered.Add(mp.SegmentIndex);
                    lastTime = mp.MidTime;
                }
            }

            return filtered
                .Distinct()
                .OrderBy(id => id)
                .ToList();
        }
    }
}
