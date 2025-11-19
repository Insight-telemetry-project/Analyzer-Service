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
        private readonly IFlightTelemetryMongoProxy _flightTelemetryMongoProxy;


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
            _flightTelemetryMongoProxy = flightTelemetryMongoProxy;
        }

        public async Task<List<SegmentClassificationResult>> ClassifyAsync(int masterIndex, string fieldName)
        {
            (List<double> timeSeries, List<double> signalSeries) =
                await LoadFlightData(masterIndex, fieldName);

            List<SegmentBoundary> detectedSegments =
                await DetectSegments(masterIndex, fieldName, signalSeries.Count);

            List<double> processedSignal = PreprocessSignal(signalSeries);

            List<double> meanValues =
                segmentLogicUtility.ComputeMeansPerSegment(processedSignal, detectedSegments);

            return segmentLogicUtility.ClassifySegments(
                timeSeries,
                processedSignal,
                detectedSegments,
                meanValues);
        }

        private async Task<(List<double> Time, List<double> Signal)> LoadFlightData(int masterIndex, string fieldName)
        {
            return await flightDataPreparer.PrepareFlightDataAsync(
                masterIndex,
                ConstantFligth.TIMESTEP_COL,
                fieldName);
        }

        private async Task<List<SegmentBoundary>> DetectSegments(int masterIndex, string fieldName, int sampleCount)
        {
            List<int> rawBoundaries =
                await changePointDetectionService.DetectChangePointsAsync(masterIndex, fieldName);

            List<int> cleaned =
                rawBoundaries
                    .Distinct()
                    .Where(Boundarie => Boundarie > 0 && Boundarie < sampleCount)
                    .OrderBy(Boundarie => Boundarie)
                    .ToList();

            if (!cleaned.Contains(sampleCount))
                cleaned.Add(sampleCount);

            return featureExtractionUtility.BuildSegmentsFromPoints(cleaned, sampleCount);
        }

        private List<double> PreprocessSignal(List<double> signal)
        {
            double[] filtered =
                signalProcessingUtility.ApplyHampel(
                    signal.ToArray(),
                    ConstantAlgorithm.HAMPEL_WINDOW,
                    ConstantAlgorithm.HAMPEL_SIGMA);

            List<double> normalized =
                signalProcessingUtility.ApplyZScore(filtered);

            return normalized;
        }

        public async Task<(List<SegmentClassificationResult> Segments, List<int> Anomalies)>
            ClassifyWithAnomaliesAsync(int masterIndex, string fieldName)
        {
            (List<double> timeSeries, List<double> signalSeries) =
                await LoadFlightData(masterIndex, fieldName);

            List<SegmentBoundary> detectedSegments =
                await DetectSegments(masterIndex, fieldName, signalSeries.Count);

            List<double> processedSignal =
                PreprocessSignal(signalSeries);

            List<double> meanValues =
                segmentLogicUtility.ComputeMeansPerSegment(processedSignal, detectedSegments);

            List<SegmentClassificationResult> merged =
                segmentLogicUtility.ClassifySegments(
                    timeSeries,
                    processedSignal,
                    detectedSegments,
                    meanValues);

            List<SegmentBoundary> mergedSegments =
                merged.Select(merg => merg.Segment).ToList();

            List<double> mergedMeans =
                segmentLogicUtility.ComputeMeansPerSegment(processedSignal, mergedSegments);

            List<Dictionary<string, double>> featureList =
                segmentLogicUtility.BuildFeatureList(timeSeries, processedSignal, mergedSegments, mergedMeans);

            List<string> labels =
                merged.Select(m => m.Label).ToList();

            List<int> anomalies =
                anomalyDetectionUtility.DetectAnomalies(
                    timeSeries,
                    processedSignal,
                    mergedSegments,
                    labels,
                    featureList);
            for (int i = 0; i < anomalies.Count; i++)
            {
                int anomalyIndex = anomalies[i];

                SegmentBoundary boundary = mergedSegments[anomalyIndex];

                double startTime = timeSeries[boundary.StartIndex];
                double endTime = timeSeries[boundary.EndIndex - 1];

                double midTime = 0.5 * (startTime + endTime);

                await _flightTelemetryMongoProxy.StoreAnomalyAsync(
                    masterIndex,
                    fieldName,
                    midTime
                );
            }
            return (merged, anomalies);
        } 
    }
}
