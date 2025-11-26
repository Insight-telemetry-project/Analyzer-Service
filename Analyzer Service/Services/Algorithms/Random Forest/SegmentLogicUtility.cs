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

        public List<double> ComputeMeansPerSegment(List<double> signalValues, List<SegmentBoundary> segmentBoundaries)
        {
            List<double> meanValuesPerSegment = new List<double>(segmentBoundaries.Count);

            for (int indexSegment = 0; indexSegment < segmentBoundaries.Count; indexSegment++)
            {
                SegmentBoundary seg = segmentBoundaries[indexSegment];

                IEnumerable<double> slice = signalValues
                    .Skip(seg.StartIndex)
                    .Take(seg.EndIndex - seg.StartIndex);

                meanValuesPerSegment.Add(slice.Average());
            }

            return meanValuesPerSegment;
        }


        public List<SegmentClassificationResult> ClassifySegments(
            List<double> timeSeriesValues,
            List<double> signalValues,
            List<SegmentBoundary> segmentBoundaries,
            List<double> meanValuesPerSegment)
        {
            List<SegmentClassificationResult> classificationResults = new List<SegmentClassificationResult>();

            for (int segmentIndex = 0; segmentIndex < segmentBoundaries.Count; segmentIndex++)
            {
                SegmentBoundary segmentBoundary = segmentBoundaries[segmentIndex];

                double previousMeanValue = segmentIndex > 0 ? meanValuesPerSegment[segmentIndex - 1] : 0.0;
                double nextMeanValue = segmentIndex < meanValuesPerSegment.Count - 1 ? meanValuesPerSegment[segmentIndex + 1] : 0.0;

                double[] featureVector =
                    featureExtractionUtility.ExtractFeatures(
                        timeSeriesValues,
                        signalValues,
                        segmentBoundary,
                        previousMeanValue,
                        nextMeanValue);

                string predictedLabel =
                    randomForestOperations.PredictLabel(modelProvider, featureVector);

                classificationResults.Add(
                    new SegmentClassificationResult(segmentBoundary, predictedLabel)
                );
            }

            return MergeSegments(classificationResults);
        }

        public List<SegmentClassificationResult> MergeSegments(List<SegmentClassificationResult> segmentClassificationResults)
        {
            List<SegmentClassificationResult> mergedSegmentResults = new List<SegmentClassificationResult>();

            SegmentClassificationResult currentSegmentResult = segmentClassificationResults[0];

            for (int segmentIndex = 1; segmentIndex < segmentClassificationResults.Count; segmentIndex++)
            {
                SegmentClassificationResult nextSegmentResult = segmentClassificationResults[segmentIndex];

                bool hasSameLabel = currentSegmentResult.Label == nextSegmentResult.Label;
                bool isContinuous =
                    currentSegmentResult.Segment.EndIndex == nextSegmentResult.Segment.StartIndex;

                if (hasSameLabel && isContinuous)
                {
                    currentSegmentResult = new SegmentClassificationResult(
                        new SegmentBoundary(
                            currentSegmentResult.Segment.StartIndex,
                            nextSegmentResult.Segment.EndIndex),
                        currentSegmentResult.Label);
                }
                else
                {
                    mergedSegmentResults.Add(currentSegmentResult);
                    currentSegmentResult = nextSegmentResult;
                }
            }

            mergedSegmentResults.Add(currentSegmentResult);
            return mergedSegmentResults;
        }

        public List<Dictionary<string, double>> BuildFeatureList(
            List<double> timeSeriesValues,
            List<double> signalValues,
            List<SegmentBoundary> segmentBoundaries,
            List<double> meanValuesPerSegment)
        {
            List<Dictionary<string, double>> featureList =
                new List<Dictionary<string, double>>(segmentBoundaries.Count);

            for (int segmentIndex = 0; segmentIndex < segmentBoundaries.Count; segmentIndex++)
            {
                double previousMeanValue = segmentIndex > 0 ? meanValuesPerSegment[segmentIndex - 1] : 0.0;
                double nextMeanValue = segmentIndex < meanValuesPerSegment.Count - 1 ? meanValuesPerSegment[segmentIndex + 1] : 0.0;

                double[] featureVector =
                    featureExtractionUtility.ExtractFeatures(
                        timeSeriesValues,
                        signalValues,
                        segmentBoundaries[segmentIndex],
                        previousMeanValue,
                        nextMeanValue);

                Dictionary<string, double> featureDictionary =
                    modelProvider.BuildFeatureDictionary(featureVector);

                featureList.Add(featureDictionary);
            }

            return featureList;
        }
    }
}
