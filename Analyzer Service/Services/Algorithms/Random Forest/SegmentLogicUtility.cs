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

        public List<double> ComputeMeansPerSegment(
            List<double> signalValues,
            List<SegmentBoundary> segmentBoundaries)
        {
            List<double> meanValuesPerSegment = new List<double>(segmentBoundaries.Count);

            for (int segmentIndex = 0; segmentIndex < segmentBoundaries.Count; segmentIndex++)
            {
                SegmentBoundary segmentBoundary = segmentBoundaries[segmentIndex];

                IEnumerable<double> segmentSlice = signalValues
                    .Skip(segmentBoundary.StartIndex)
                    .Take(segmentBoundary.EndIndex - segmentBoundary.StartIndex);

                double meanValue = segmentSlice.Average();
                meanValuesPerSegment.Add(meanValue);
            }

            return meanValuesPerSegment;
        }

        public List<SegmentClassificationResult> ClassifySegments(
            List<double> timeSeriesValues,
            List<double> signalValues,
            List<SegmentBoundary> segmentBoundaries,
            List<double> meanValuesPerSegment)
        {
            List<SegmentClassificationResult> classificationResults =
                new List<SegmentClassificationResult>();

            RandomForestModel model = modelProvider.GetModel();

            for (int segmentIndex = 0; segmentIndex < segmentBoundaries.Count; segmentIndex++)
            {
                SegmentBoundary segmentBoundary = segmentBoundaries[segmentIndex];

                double previousMeanValue =
                    segmentIndex > 0 ? meanValuesPerSegment[segmentIndex - 1] : 0.0;

                double nextMeanValue =
                    segmentIndex < meanValuesPerSegment.Count - 1
                        ? meanValuesPerSegment[segmentIndex + 1]
                        : 0.0;

                double[] featureVector =
                    featureExtractionUtility.ExtractFeatures(
                        timeSeriesValues,
                        signalValues,
                        segmentBoundary,
                        previousMeanValue,
                        nextMeanValue);

                string predictedLabel =
                    randomForestOperations.PredictLabel(model, featureVector);

                SegmentClassificationResult result =
                    new SegmentClassificationResult(segmentBoundary, predictedLabel);

                classificationResults.Add(result);
            }

            return MergeSegments(classificationResults);
        }

        public List<SegmentClassificationResult> MergeSegments(
            List<SegmentClassificationResult> classificationResults)
        {
            List<SegmentClassificationResult> mergedResults =
                new List<SegmentClassificationResult>();

            SegmentClassificationResult currentSegment = classificationResults[0];

            for (int index = 1; index < classificationResults.Count; index++)
            {
                SegmentClassificationResult nextSegment = classificationResults[index];

                bool labelsMatch = currentSegment.Label == nextSegment.Label;
                bool segmentsAreContinuous =
                    currentSegment.Segment.EndIndex == nextSegment.Segment.StartIndex;

                if (labelsMatch && segmentsAreContinuous)
                {
                    currentSegment =
                        new SegmentClassificationResult(
                            new SegmentBoundary(
                                currentSegment.Segment.StartIndex,
                                nextSegment.Segment.EndIndex),
                            currentSegment.Label);
                }
                else
                {
                    mergedResults.Add(currentSegment);
                    currentSegment = nextSegment;
                }
            }

            mergedResults.Add(currentSegment);
            return mergedResults;
        }

        public List<Dictionary<string, double>> BuildFeatureList(
            List<double> timeSeriesValues,
            List<double> signalValues,
            List<SegmentBoundary> segmentBoundaries,
            List<double> meanValuesPerSegment)
        {
            List<Dictionary<string, double>> featureList =
                new List<Dictionary<string, double>>(segmentBoundaries.Count);

            RandomForestModel model = modelProvider.GetModel();

            for (int segmentIndex = 0; segmentIndex < segmentBoundaries.Count; segmentIndex++)
            {
                double previousMeanValue =
                    segmentIndex > 0 ? meanValuesPerSegment[segmentIndex - 1] : 0.0;

                double nextMeanValue =
                    segmentIndex < meanValuesPerSegment.Count - 1
                        ? meanValuesPerSegment[segmentIndex + 1]
                        : 0.0;

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
