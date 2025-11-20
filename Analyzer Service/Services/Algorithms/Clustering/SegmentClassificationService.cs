using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms.AnomalyDetector;
using Analyzer_Service.Models.Interface.Algorithms.Clustering;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Algorithms.Random_Forest;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Services.Mongo;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Analyzer_Service.Services
{
    public class SegmentClassificationService : ISegmentClassificationService
    {
        private readonly IPrepareFlightData flightDataPreparer;
        private readonly IChangePointDetectionService changePointDetectionService;
        private readonly ISignalProcessingUtility signalProcessingUtility;
        private readonly IFeatureExtractionUtility featureExtractionUtility;
        private readonly IAnomalyDetectionUtility anomalyDetectionUtility;
        private readonly ISegmentLogicUtility segmentLogicUtility;
        private readonly IFlightTelemetryMongoProxy flightTelemetryMongoProxy;

        public SegmentClassificationService(
            IPrepareFlightData flightDataPreparer,
            IChangePointDetectionService changePointDetectionService,
            ISignalProcessingUtility signalProcessingUtility,
            IFeatureExtractionUtility featureExtractionUtility,
            IAnomalyDetectionUtility anomalyDetectionUtility,
            IRandomForestModelProvider modelProvider,
            IRandomForestOperations randomForestOperations,
            ISegmentLogicUtility segmentLogicUtility,
            IFlightTelemetryMongoProxy flightTelemetryMongoProxy)
        {
            this.flightDataPreparer = flightDataPreparer;
            this.changePointDetectionService = changePointDetectionService;
            this.signalProcessingUtility = signalProcessingUtility;
            this.featureExtractionUtility = featureExtractionUtility;
            this.anomalyDetectionUtility = anomalyDetectionUtility;
            this.segmentLogicUtility = segmentLogicUtility;
            this.flightTelemetryMongoProxy = flightTelemetryMongoProxy;
        }

        public async Task<List<SegmentClassificationResult>> ClassifyAsync(
            int masterIndex,
            string fieldName)
        {
            (List<double> timeSeriesValues, List<double> signalValues) =
                await LoadFlightData(masterIndex, fieldName);

            List<SegmentBoundary> detectedSegments =
                await DetectSegments(masterIndex, fieldName, signalValues.Count);

            List<double> processedSignal =
                PreprocessSignal(signalValues);

            List<double> meanValuesPerSegment =
                segmentLogicUtility.ComputeMeansPerSegment(processedSignal, detectedSegments);

            return segmentLogicUtility.ClassifySegments(
                timeSeriesValues,
                processedSignal,
                detectedSegments,
                meanValuesPerSegment);
        }

        private async Task<(List<double> Time, List<double> Signal)> LoadFlightData(
            int masterIndex,
            string fieldName)
        {
            return await flightDataPreparer.PrepareFlightDataAsync(
                masterIndex,
                ConstantFligth.TIMESTEP_COL,
                fieldName);
        }

        private async Task<List<SegmentBoundary>> DetectSegments(
            int masterIndex,
            string fieldName,
            int totalSampleCount)
        {
            List<int> rawChangePoints =
                await changePointDetectionService.DetectChangePointsAsync(masterIndex, fieldName);

            List<int> cleanedChangePoints =
                rawChangePoints
                    .Distinct()
                    .Where(changePoint => changePoint > 0 && changePoint < totalSampleCount)
                    .OrderBy(changePoint => changePoint)
                    .ToList();

            if (!cleanedChangePoints.Contains(totalSampleCount))
            {
                cleanedChangePoints.Add(totalSampleCount);
            }

            return featureExtractionUtility.BuildSegmentsFromPoints(
                cleanedChangePoints,
                totalSampleCount);
        }

        private List<double> PreprocessSignal(List<double> signalValues)
        {
            double[] filteredSignal =
                signalProcessingUtility.ApplyHampel(
                    signalValues.ToArray(),
                    ConstantAlgorithm.HAMPEL_WINDOW,
                    ConstantAlgorithm.HAMPEL_SIGMA);

            List<double> normalizedSignal =
                signalProcessingUtility.ApplyZScore(filteredSignal);

            return normalizedSignal;
        }

        public async Task<(List<SegmentClassificationResult> Segments, List<int> Anomalies)>
            ClassifyWithAnomaliesAsync(
                int masterIndex,
                string fieldName,
                int startIndex,
                int endIndex)
        {
            (List<double> timeSeriesValues, List<double> signalValues) =
                await LoadFlightData(masterIndex, fieldName);

            if (!(startIndex == 0 && endIndex == 0))
            {
                if (startIndex < 0)
                    startIndex = 0;

                if (endIndex > signalValues.Count - 1)
                    endIndex = signalValues.Count - 1;

                if (startIndex >= endIndex)
                    return (new List<SegmentClassificationResult>(), new List<int>());

                timeSeriesValues = timeSeriesValues
                    .Skip(startIndex)
                    .Take(endIndex - startIndex + 1)
                    .ToList();

                signalValues = signalValues
                    .Skip(startIndex)
                    .Take(endIndex - startIndex + 1)
                    .ToList();
            }

            List<SegmentBoundary> detectedSegments =
                await DetectSegments(masterIndex, fieldName, signalValues.Count);

            List<double> processedSignal =
                PreprocessSignal(signalValues);

            List<double> meanValuesPerSegment =
                segmentLogicUtility.ComputeMeansPerSegment(processedSignal, detectedSegments);

            List<SegmentClassificationResult> mergedSegmentResults =
                segmentLogicUtility.ClassifySegments(
                    timeSeriesValues,
                    processedSignal,
                    detectedSegments,
                    meanValuesPerSegment);

            List<SegmentBoundary> mergedSegments =
                mergedSegmentResults.Select(result => result.Segment).ToList();

            List<double> mergedMeanValues =
                segmentLogicUtility.ComputeMeansPerSegment(processedSignal, mergedSegments);

            List<Dictionary<string, double>> featureList =
                segmentLogicUtility.BuildFeatureList(
                    timeSeriesValues,
                    processedSignal,
                    mergedSegments,
                    mergedMeanValues);

            List<string> predictedLabels =
                mergedSegmentResults.Select(result => result.Label).ToList();

            List<int> detectedAnomalies =
                anomalyDetectionUtility.DetectAnomalies(
                    timeSeriesValues,
                    processedSignal,
                    mergedSegments,
                    predictedLabels,
                    featureList);

            for (int anomalyIndex = 0; anomalyIndex < detectedAnomalies.Count; anomalyIndex++)
            {
                int anomalySegmentIndex = detectedAnomalies[anomalyIndex];
                SegmentBoundary anomalyBoundary = mergedSegments[anomalySegmentIndex];

                double anomalyStartTime = timeSeriesValues[anomalyBoundary.StartIndex];
                double anomalyEndTime = timeSeriesValues[anomalyBoundary.EndIndex - 1];

                double anomalyMidpointTime = 0.5 * (anomalyStartTime + anomalyEndTime);

                await flightTelemetryMongoProxy.StoreAnomalyAsync(
                    masterIndex,
                    fieldName,
                    anomalyMidpointTime);
            }

            return (mergedSegmentResults, detectedAnomalies);
        }
    }
}
