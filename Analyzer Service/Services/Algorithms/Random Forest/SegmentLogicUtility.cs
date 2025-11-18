using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms.Random_Forest;

namespace Analyzer_Service.Services.Algorithms.Random_Forest
{
    public class SegmentLogicUtility : ISegmentLogicUtility
    {
        private readonly IRandomForestModelProvider modelProvider;
        private readonly IRandomForestOperations randomForestOperations;
        private readonly IFeatureExtractionUtility featureExtractionUtility;

        public SegmentLogicUtility(
            IRandomForestModelProvider modelProvider,
            IRandomForestOperations randomForestOperations,
            IFeatureExtractionUtility featureExtractionUtility)
        {
            this.modelProvider = modelProvider;
            this.randomForestOperations = randomForestOperations;
            this.featureExtractionUtility = featureExtractionUtility;
        }

        public List<double> ComputeMeansPerSegment(List<double> signal, List<SegmentBoundary> segments)
        {
            List<double> means = new List<double>(segments.Count);

            for (int index = 0; index < segments.Count; index++)
            {
                SegmentBoundary seg = segments[index];
                double sum = 0.0;

                for (int idx = seg.StartIndex; idx < seg.EndIndex; idx++)
                    sum += signal[idx];

                double mean = sum / (seg.EndIndex - seg.StartIndex);
                means.Add(mean);
            }

            return means;
        }

        public List<SegmentClassificationResult> ClassifySegments(
            List<double> timeSeries,
            List<double> signal,
            List<SegmentBoundary> segments,
            List<double> meanValues)
        {
            List<SegmentClassificationResult> list = new List<SegmentClassificationResult>();

            for (int index = 0; index < segments.Count; index++)
            {
                SegmentBoundary seg = segments[index];

                double prev = index > 0 ? meanValues[index - 1] : 0.0;
                double next = index < meanValues.Count - 1 ? meanValues[index + 1] : 0.0;

                double[] features =
                    featureExtractionUtility.ExtractFeatures(timeSeries, signal, seg, prev, next);

                string label =
                    randomForestOperations.PredictLabel(modelProvider, features);

                list.Add(new SegmentClassificationResult(seg, label));
            }

            return MergeSegments(list);
        }

        public List<SegmentClassificationResult> MergeSegments(List<SegmentClassificationResult> segments)
        {
            List<SegmentClassificationResult> merged = new List<SegmentClassificationResult>();

            SegmentClassificationResult current = segments[0];

            for (int index = 1; index < segments.Count; index++)
            {
                SegmentClassificationResult next = segments[index];

                if (current.Label == next.Label &&
                    current.Segment.EndIndex == next.Segment.StartIndex)
                {
                    current = new SegmentClassificationResult(
                        new SegmentBoundary(current.Segment.StartIndex, next.Segment.EndIndex),
                        current.Label);
                }
                else
                {
                    merged.Add(current);
                    current = next;
                }
            }

            merged.Add(current);
            return merged;
        }
        public List<Dictionary<string, double>> BuildFeatureList(
            List<double> timeSeries,
            List<double> signal,
            List<SegmentBoundary> segments,
            List<double> meanValues)
        {
            List<Dictionary<string, double>> list =
                new List<Dictionary<string, double>>(segments.Count);

            for (int index = 0; index < segments.Count; index++)
            {
                double prev = index > 0 ? meanValues[index - 1] : 0.0;
                double next = index < meanValues.Count - 1 ? meanValues[index + 1] : 0.0;

                double[] vector =
                    featureExtractionUtility.ExtractFeatures(
                        timeSeries,
                        signal,
                        segments[index],
                        prev,
                        next);

                Dictionary<string, double> dict =
                    modelProvider.BuildFeatureDictionary(vector);

                list.Add(dict);
            }

            return list;
        }
    }
}
