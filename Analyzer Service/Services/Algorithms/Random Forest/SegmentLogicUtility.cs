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

        public double[] ComputeMeansPerSegment(double[] signalValues, List<SegmentBoundary> segmentBoundaries)
        {
            int segmentCount = segmentBoundaries.Count;
            double[] meanValuesPerSegment = new double[segmentCount];

            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                SegmentBoundary segmentBoundary = segmentBoundaries[segmentIndex];

                int startIndex = segmentBoundary.StartIndex;
                int endIndex = segmentBoundary.EndIndex;

                int segmentLength = endIndex - startIndex;
                if (segmentLength <= 0)
                {
                    meanValuesPerSegment[segmentIndex] = 0.0;
                    continue;
                }

                double sum = 0.0;
                for (int sampleIndex = startIndex; sampleIndex < endIndex; sampleIndex++)
                {
                    sum += signalValues[sampleIndex];
                }

                meanValuesPerSegment[segmentIndex] = sum / segmentLength;
            }

            return meanValuesPerSegment;
        }



        public List<SegmentClassificationResult> ClassifySegments(
            double[] signalValues,
            List<SegmentBoundary> segmentBoundaries,
            double[] meanValuesPerSegment)
        {
            List<SegmentClassificationResult> classificationResults =
                new List<SegmentClassificationResult>(segmentBoundaries.Count);

            RandomForestModel model = modelProvider.GetModel();

            for (int segmentIndex = 0; segmentIndex < segmentBoundaries.Count; segmentIndex++)
            {
                SegmentBoundary segmentBoundary = segmentBoundaries[segmentIndex];

                double previousMeanValue =
                    segmentIndex > 0 ? meanValuesPerSegment[segmentIndex - 1] : 0.0;

                double nextMeanValue =
                    segmentIndex < meanValuesPerSegment.Length - 1
                        ? meanValuesPerSegment[segmentIndex + 1]
                        : 0.0;

                SegmentFeatures features =
                    featureExtractionUtility.ExtractFeatures(
                        signalValues,
                        segmentBoundary,
                        previousMeanValue,
                        nextMeanValue);

                string predictedLabel = randomForestOperations.PredictLabel(model, features);

                SegmentClassificationResult result = new SegmentClassificationResult(segmentBoundary, predictedLabel);
                classificationResults.Add(result);
            }

            return MergeSegments(classificationResults);
        }

        public List<SegmentClassificationResult> MergeSegments(List<SegmentClassificationResult> classificationResults)
        {
            if (classificationResults == null || classificationResults.Count == 0)
            {
                return new List<SegmentClassificationResult>();
            }

            List<SegmentClassificationResult> mergedResults =
                new List<SegmentClassificationResult>(classificationResults.Count);

            SegmentClassificationResult currentSegment = classificationResults[0];

            for (int resultIndex = 1; resultIndex < classificationResults.Count; resultIndex++)
            {
                SegmentClassificationResult nextSegment = classificationResults[resultIndex];

                bool labelsMatch = currentSegment.Label == nextSegment.Label;
                bool segmentsAreContinuous = currentSegment.Segment.EndIndex == nextSegment.Segment.StartIndex;

                if (labelsMatch && segmentsAreContinuous)
                {
                    SegmentBoundary mergedBoundary =
                        new SegmentBoundary(
                            currentSegment.Segment.StartIndex,
                            nextSegment.Segment.EndIndex);

                    currentSegment = new SegmentClassificationResult(mergedBoundary, currentSegment.Label);
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


        public List<SegmentFeatures> BuildFeatureList(
            double[] signalValues,
            List<SegmentBoundary> segmentBoundaries,
            double[] meanValuesPerSegment)
        {
            List<SegmentFeatures> featureList = new List<SegmentFeatures>(segmentBoundaries.Count);

            for (int segmentIndex = 0; segmentIndex < segmentBoundaries.Count; segmentIndex++)
            {
                double previousMean =
                    segmentIndex > 0 ? meanValuesPerSegment[segmentIndex - 1] : 0.0;

                double nextMean =
                    segmentIndex < meanValuesPerSegment.Length - 1 ? meanValuesPerSegment[segmentIndex + 1] : 0.0;

                SegmentFeatures features =
                    featureExtractionUtility.ExtractFeatures(
                        signalValues,
                        segmentBoundaries[segmentIndex],
                        previousMean,
                        nextMean);

                featureList.Add(features);
            }

            return featureList;
        }
    }
}
