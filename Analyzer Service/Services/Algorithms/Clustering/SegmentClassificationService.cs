using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms.Clustering;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Services.Algorithms.Clustering;
using System;
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

        private const int HampelWindow = 21;
        private const double HampelSigma = 3.0;

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

            string jsonText = File.ReadAllText("Models/Ml/E1_EGT1_rf.json");
            this.modelDocument = JsonDocument.Parse(jsonText);
        }

        public async Task<List<SegmentClassificationResult>> ClassifyAsync(int masterIndex, string fieldName)
        {
            (List<double> timeSeries, List<double> signalSeries) =
                await flightDataPreparer.PrepareFlightDataAsync(
                    masterIndex,
                    "timestep",
                    fieldName);

            int sampleCount = signalSeries.Count;

            List<int> detectedChangePoints =
                await changePointDetectionService.DetectChangePointsAsync(
                    masterIndex,
                    fieldName);

            detectedChangePoints = detectedChangePoints
                .Distinct()
                .Where(boundary => boundary > 0 && boundary < sampleCount)
                .OrderBy(boundary => boundary)
                .ToList();

            if (!detectedChangePoints.Contains(sampleCount))
            {
                detectedChangePoints.Add(sampleCount);
            }

            List<(int StartIndex, int EndIndex)> segmentBoundaries =
                featureExtractionUtility.BuildSegments(
                    detectedChangePoints,
                    sampleCount);

            double[] filteredSignal =
                signalProcessingUtility.ApplyHampel(
                    signalSeries.ToArray(),
                    HampelWindow,
                    HampelSigma);

            List<double> normalizedSignal =
                signalProcessingUtility.ApplyZScore(
                    filteredSignal);

            List<double> meanPerSegment =
                new List<double>(segmentBoundaries.Count);

            for (int segmentIndex = 0; segmentIndex < segmentBoundaries.Count; segmentIndex++)
            {
                int startIndex = segmentBoundaries[segmentIndex].StartIndex;
                int endIndex = segmentBoundaries[segmentIndex].EndIndex;

                double meanValue =
                    signalProcessingUtility.ComputeMean(
                        normalizedSignal,
                        startIndex,
                        endIndex);

                meanPerSegment.Add(meanValue);
            }

            List<SegmentClassificationResult> resultList =
                new List<SegmentClassificationResult>(segmentBoundaries.Count);

            for (int segmentIndex = 0; segmentIndex < segmentBoundaries.Count; segmentIndex++)
            {
                int startIndex = segmentBoundaries[segmentIndex].StartIndex;
                int endIndex = segmentBoundaries[segmentIndex].EndIndex;

                double previousMean =
                    segmentIndex > 0
                    ? meanPerSegment[segmentIndex - 1]
                    : 0.0;

                double nextMean =
                    segmentIndex < meanPerSegment.Count - 1
                    ? meanPerSegment[segmentIndex + 1]
                    : 0.0;

                double[] featureVector =
                    featureExtractionUtility.ExtractFeatures(
                        timeSeries,
                        normalizedSignal,
                        startIndex,
                        endIndex,
                        previousMean,
                        nextMean);

                string predictedLabel =
                    MinimalRF.PredictLabel(modelDocument, featureVector);

                SegmentClassificationResult classificationResult =
                    new SegmentClassificationResult(
                        new SegmentBoundary(startIndex, endIndex),
                        predictedLabel);

                resultList.Add(classificationResult);
            }

            return resultList;
        }
    }
}
