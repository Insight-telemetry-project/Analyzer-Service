using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms.AnomalyDetector;
using Analyzer_Service.Models.Interface.Algorithms.Clustering;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Algorithms.Random_Forest;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Models.Schema;
using Analyzer_Service.Services.Algorithms.AnomalyDetector;
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
        private readonly IPatternHashingUtility patternHashingUtility;

        public SegmentClassificationService(
            IPrepareFlightData flightDataPreparer,
            IChangePointDetectionService changePointDetectionService,
            ISignalProcessingUtility signalProcessingUtility,
            IFeatureExtractionUtility featureExtractionUtility,
            IAnomalyDetectionUtility anomalyDetectionUtility,
            IRandomForestModelProvider modelProvider,
            IRandomForestOperations randomForestOperations,
            ISegmentLogicUtility segmentLogicUtility,
            IFlightTelemetryMongoProxy flightTelemetryMongoProxy,
            IPatternHashingUtility patternHashingUtility)
        {
            this.flightDataPreparer = flightDataPreparer;
            this.changePointDetectionService = changePointDetectionService;
            this.signalProcessingUtility = signalProcessingUtility;
            this.featureExtractionUtility = featureExtractionUtility;
            this.anomalyDetectionUtility = anomalyDetectionUtility;
            this.segmentLogicUtility = segmentLogicUtility;
            this.flightTelemetryMongoProxy = flightTelemetryMongoProxy;
            this.patternHashingUtility = patternHashingUtility;
        }

        public async Task<List<SegmentClassificationResult>> ClassifyAsync(
            int masterIndex,
            string fieldName)
        {
            SignalSeries series = await LoadFlightData(masterIndex, fieldName);

            List<double> timeSeriesValues = series.Time;
            List<double> signalValues = series.Values;


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

        private async Task<SignalSeries> LoadFlightData(
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
            SignalSeries series = await LoadFlightData(masterIndex, fieldName);

            List<double> timeSeriesValues = series.Time;
            List<double> signalValues = series.Values;


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


            for (int i = 0; i < mergedSegmentResults.Count; i++)
            {
                mergedSegmentResults[i].FeatureValues = featureList[i];
            }

            for (int i = 0; i < mergedSegmentResults.Count; i++)
            {
                string hash = patternHashingUtility.ComputeHash(
                    timeSeriesValues,
                    processedSignal,
                    mergedSegments[i]);

                mergedSegmentResults[i].HashVector =
                    hash.Split(',').Select(double.Parse).ToArray();
            }


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
                int segmentIndex = detectedAnomalies[anomalyIndex];
                SegmentBoundary boundary = mergedSegments[segmentIndex];

                double startTime = timeSeriesValues[boundary.StartIndex];
                double endTime = timeSeriesValues[boundary.EndIndex - 1];

                double midpoint = 0.5 * (startTime + endTime);

                await flightTelemetryMongoProxy.StoreAnomalyAsync(
                    masterIndex,
                    fieldName,
                    midpoint);

                string label = predictedLabels[segmentIndex];
                Dictionary<string, double> features = featureList[segmentIndex];

                string hash = patternHashingUtility.ComputeHash(
                    timeSeriesValues,
                    processedSignal,
                    boundary);

                HistoricalAnomalyRecord record = new HistoricalAnomalyRecord
                {
                    MasterIndex = masterIndex,
                    ParameterName = fieldName,
                    StartIndex = boundary.StartIndex,
                    EndIndex = boundary.EndIndex,
                    Label = label,
                    PatternHash = hash,
                    FeatureValues = features,
                    CreatedAt = DateTime.UtcNow
                };

                await flightTelemetryMongoProxy.StoreHistoricalAnomalyAsync(record);
            }


            return (mergedSegmentResults, detectedAnomalies);
        }
    }
}
