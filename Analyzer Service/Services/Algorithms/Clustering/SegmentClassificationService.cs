using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms.Clustering;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Services.Algorithms.Clustering;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Analyzer_Service.Services
{
    public class SegmentClassificationService : ISegmentClassificationService
    {
        private readonly IPrepareFlightData flightDataPreparer;
        private readonly IChangePointDetectionService changePointDetectionService;
        private readonly ISignalProcessingUtility signalProcessingUtility;
        private readonly IFeatureExtractionUtility featureExtractionUtility;
        private readonly JsonDocument modelDocument;

        public SegmentClassificationService(
            IPrepareFlightData flightDataPreparer,
            IChangePointDetectionService changePointDetectionService,
            ISignalProcessingUtility signalProcessingUtility,
            IFeatureExtractionUtility featureExtractionUtility)
        {
            this.flightDataPreparer = flightDataPreparer;
            this.changePointDetectionService = changePointDetectionService;
            this.signalProcessingUtility = signalProcessingUtility;
            this.featureExtractionUtility = featureExtractionUtility;

            string jsonContent = File.ReadAllText(ConstantRandomForest.ML_FILE_PATH);
            this.modelDocument = JsonDocument.Parse(jsonContent);
        }

        public async Task<List<SegmentClassificationResult>> ClassifyAsync(int masterIndex, string fieldName)
        {
            (List<double> timeSeries, List<double> signalSeries) =await LoadFlightData(masterIndex, fieldName);

            List<SegmentBoundary> detectedSegments =await DetectSegments(masterIndex, fieldName, signalSeries.Count);

            List<double> normalizedSignal =PreprocessSignal(signalSeries);

            List<double> meanValues = ComputeMeansPerSegment(normalizedSignal, detectedSegments);

            List<SegmentClassificationResult> results =
                ClassifySegments(timeSeries, normalizedSignal, detectedSegments, meanValues);

            return results;
        }

        private async Task<(List<double> TimeSeries, List<double> SignalSeries)> LoadFlightData(int masterIndex, string fieldName)
        {
            (List<double> TimeSeries, List<double> SignalSeries) =
                await flightDataPreparer.PrepareFlightDataAsync(masterIndex,ConstantFligth.TIMESTEP_COL,fieldName);

            return (TimeSeries, SignalSeries);
        }

        private async Task<List<SegmentBoundary>> DetectSegments(int masterIndex, string fieldName, int sampleCount)
        {
            List<int> rawBoundaries =
                await changePointDetectionService.DetectChangePointsAsync(masterIndex,fieldName);

            List<int> cleanedBoundaries =
                rawBoundaries
                .Distinct()
                .Where(boundary => boundary > 0 && boundary < sampleCount)
                .OrderBy(boundary => boundary)
                .ToList();

            if (!cleanedBoundaries.Contains(sampleCount))
            {
                cleanedBoundaries.Add(sampleCount);
            }

            List<SegmentBoundary> segments = featureExtractionUtility.BuildSegmentsFromPoints(cleanedBoundaries, sampleCount);

            return segments;
        }

        private List<double> PreprocessSignal(List<double> signal)
        {
            double[] filteredValues =
                signalProcessingUtility.ApplyHampel(signal.ToArray(),ConstantAlgorithm.HAMPEL_WINDOW,ConstantAlgorithm.HAMPEL_SIGMA);

            List<double> normalizedValues = signalProcessingUtility.ApplyZScore(filteredValues);

            return normalizedValues;
        }

        private List<double> ComputeMeansPerSegment(List<double> normalizedSignal, List<SegmentBoundary> segments)
        {
            List<double> meanValues = new List<double>(segments.Count);

            for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
            {
                int startIndex = segments[segmentIndex].StartIndex;
                int endIndex = segments[segmentIndex].EndIndex;

                double segmentMean =signalProcessingUtility.ComputeMean(normalizedSignal,startIndex,endIndex);

                meanValues.Add(segmentMean);
            }

            return meanValues;
        }

        private List<SegmentClassificationResult> ClassifySegments(List<double> timeSeries,List<double> normalizedSignal,
            List<SegmentBoundary> segments, List<double> meanValues)
        {
            List<SegmentClassificationResult> classificationResults =
                new List<SegmentClassificationResult>(segments.Count);



            for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
            {
                int startIndex = segments[segmentIndex].StartIndex;
                int endIndex = segments[segmentIndex].EndIndex;

                double previousMean =
                    segmentIndex > 0
                    ? meanValues[segmentIndex - 1]
                    : 0.0;

                double nextMean =
                    segmentIndex < meanValues.Count - 1
                    ? meanValues[segmentIndex + 1]
                    : 0.0;

                SegmentBoundary currentSegment = segments[segmentIndex];

                double[] featureVector =
                    featureExtractionUtility.ExtractFeatures(
                        timeSeries,
                        normalizedSignal,
                        currentSegment,
                        previousMean,
                        nextMean);

                string predictedLabel =
                    MinimalRF.PredictLabel(modelDocument, featureVector);

                SegmentClassificationResult result =
                    new SegmentClassificationResult(
                        new SegmentBoundary(startIndex, endIndex),
                        predictedLabel);

                classificationResults.Add(result);
            }
            classificationResults = MergeSegments(classificationResults);
            return classificationResults;
        }
        private List<SegmentClassificationResult> MergeSegments(List<SegmentClassificationResult> segments)
        {

            List<SegmentClassificationResult> merged = new List<SegmentClassificationResult>();

            SegmentClassificationResult current = segments[0];

            for (int index = 1; index < segments.Count; index++)
            {
                SegmentClassificationResult next = segments[index];

                bool sameLabel = next.Label == current.Label;
                bool adjacent = next.Segment.StartIndex == current.Segment.EndIndex;

                if (sameLabel && adjacent)
                {
                    current = new SegmentClassificationResult(
                        new SegmentBoundary(current.Segment.StartIndex, next.Segment.EndIndex),
                        current.Label
                    );
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

    }
}
