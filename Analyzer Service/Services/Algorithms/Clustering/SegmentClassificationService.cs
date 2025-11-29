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

        public async Task<List<SegmentClassificationResult>> ClassifyAsync(int masterIndex,string fieldName)
        {
            SignalSeries series = await LoadFlightData(masterIndex, fieldName);

            List<double> timeSeriesValues = series.Time;
            List<double> signalValues = series.Values;


            List<SegmentBoundary> detectedSegments =await DetectSegments(masterIndex, fieldName, signalValues.Count);

            List<double> processedSignal =PreprocessSignal(signalValues);

            List<double> meanValuesPerSegment =segmentLogicUtility.ComputeMeansPerSegment(processedSignal, detectedSegments);

            return segmentLogicUtility.ClassifySegments(
                timeSeriesValues,
                processedSignal,
                detectedSegments,
                meanValuesPerSegment);
        }

        private async Task<SignalSeries> LoadFlightData(int masterIndex,string fieldName)
        {
            return await flightDataPreparer.PrepareFlightDataAsync(masterIndex,ConstantFligth.TIMESTEP_COL,fieldName);
        }

        private async Task<List<SegmentBoundary>> DetectSegments(int masterIndex,string fieldName,int totalSampleCount)
        {
            List<int> rawChangePoints =await changePointDetectionService.DetectChangePointsAsync(masterIndex, fieldName);

            List<int> cleanedChangePoints =rawChangePoints
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
            //double[] filteredSignal =
            //    signalProcessingUtility.ApplyHampel(
            //        signalValues.ToArray(),
            //        ConstantAlgorithm.HAMPEL_WINDOW,
            //        ConstantAlgorithm.HAMPEL_SIGMA);

            double[] filteredSignal = signalValues.ToArray();

            List<double> normalizedSignal =
                signalProcessingUtility.ApplyZScore(filteredSignal);

            return normalizedSignal;
        }



        public async Task<SegmentAnalysisResult> ClassifyWithAnomaliesAsync(
        int masterIndex, string fieldName, int startIndex, int endIndex)
        {
            constantPelt();
            (List<double> timeSeriesValues, List<double> signalValues) = await LoadRangeAsync(masterIndex, fieldName, startIndex, endIndex);

            List<SegmentBoundary> detectedSegments = await DetectSegments(masterIndex, fieldName, signalValues.Count);

            List<double> processedSignal = PreprocessSignal(signalValues);

            List<SegmentClassificationResult> mergedSegmentResults =
                BuildMergedSegmentResults(timeSeriesValues, processedSignal, detectedSegments);

            List<SegmentBoundary> mergedSegments =
                mergedSegmentResults.Select(result => result.Segment).ToList();

            List<Dictionary<string, double>> featureList =
                BuildFeatureList(timeSeriesValues, processedSignal, mergedSegments);

            AttachFeaturesToResults(mergedSegmentResults, featureList);


            AttachHashVectors(timeSeriesValues, processedSignal, mergedSegmentResults);

            //List<int> detectedAnomalies =
            //    anomalyDetectionUtility.DetectAnomalies(timeSeriesValues, processedSignal, mergedSegments,
            //                                            mergedSegmentResults.Select(segment => segment.Label).ToList(),featureList);
            List<int> detectedAnomalies =
    anomalyDetectionUtility.DetectAnomalies(
        timeSeriesValues,
        processedSignal,
        mergedSegments,
        mergedSegmentResults.Select(segment => segment.Label).ToList(),
        featureList);

            List<int> anomalySampleIndexes = new List<int>();

            foreach (int segIndex in detectedAnomalies)
            {
                SegmentBoundary boundary = mergedSegments[segIndex];
                string label = mergedSegmentResults[segIndex].Label;

                int repIndex = PickRepresentativePoint(processedSignal, boundary, label);

                anomalySampleIndexes.Add(repIndex);
            }

            await StoreAnomaliesAsync(masterIndex, fieldName, timeSeriesValues, processedSignal,mergedSegments,
                mergedSegmentResults, detectedAnomalies, featureList);

            return new SegmentAnalysisResult
            {
                //MasterIndex = masterIndex,
                //FieldName = fieldName,
                //TimeSeries = timeSeriesValues,
                //Signal = signalValues,
                //ProcessedSignal = processedSignal,
                Segments = mergedSegmentResults,
                SegmentBoundaries = mergedSegments,
                //FeatureList = featureList,
                AnomalyIndexes = anomalySampleIndexes
            };

        }

        private async Task<(List<double> Time, List<double> Signal)> LoadRangeAsync(int masterIndex, string fieldName, int startIndex, int endIndex)
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

                if (startIndex >= endIndex)return (new List<double>(), new List<double>());

                timeSeriesValues = timeSeriesValues.Skip(startIndex).Take(endIndex - startIndex + 1).ToList();
                signalValues = signalValues.Skip(startIndex).Take(endIndex - startIndex + 1).ToList();
            }

            return (timeSeriesValues, signalValues);
        }

        private List<SegmentClassificationResult> BuildMergedSegmentResults(List<double> timeSeriesValues,List<double> processedSignal,
            List<SegmentBoundary> detectedSegments)
        {
            List<double> meanValues =segmentLogicUtility.ComputeMeansPerSegment(processedSignal, detectedSegments);

            return segmentLogicUtility.ClassifySegments(timeSeriesValues, processedSignal, detectedSegments, meanValues);
        }

        private List<Dictionary<string, double>> BuildFeatureList(List<double> timeSeriesValues,List<double> processedSignal,
            List<SegmentBoundary> segments)
        {
            List<double> meanValues = segmentLogicUtility.ComputeMeansPerSegment(processedSignal, segments);

            return segmentLogicUtility.BuildFeatureList(timeSeriesValues, processedSignal, segments, meanValues);
        }

        private void AttachFeaturesToResults(List<SegmentClassificationResult> results,List<Dictionary<string, double>> featureList)
        {
            for (int indexFeature = 0; indexFeature < results.Count; indexFeature++)
                results[indexFeature].FeatureValues = featureList[indexFeature];
        }

        private void AttachHashVectors(List<double> timeSeriesValues,List<double> processedSignal,List<SegmentClassificationResult> results)
        {
            for (int indexSegment = 0; indexSegment < results.Count; indexSegment++)
            {
                string hash = patternHashingUtility.ComputeHash(timeSeriesValues,processedSignal,results[indexSegment].Segment);

                results[indexSegment].HashVector = hash.Split(',').Select(double.Parse).ToArray();
            }
        }

        private async Task StoreAnomaliesAsync(int masterIndex,string fieldName,List<double> timeSeriesValues,List<double> processedSignal,
            List<SegmentBoundary> segments,
            List<SegmentClassificationResult> results,
            List<int> anomalyIndexes,
            List<Dictionary<string, double>> featureList)
        {
            for (int indexForIndex = 0; indexForIndex < anomalyIndexes.Count; indexForIndex++)
            {
                int index = anomalyIndexes[indexForIndex];
                SegmentBoundary boundary = segments[index];
                
                string label = results[index].Label;


                int repIndex = PickRepresentativePoint(processedSignal, boundary, label);
                double repTime = timeSeriesValues[repIndex];

                await flightTelemetryMongoProxy.StoreAnomalyAsync(masterIndex, fieldName, repTime);


                Dictionary<string, double> features = featureList[index];

                string hash = patternHashingUtility.ComputeHash(timeSeriesValues, processedSignal, boundary);

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
        }
        public void constantPelt()
        {
            ConstantPelt.SAMPLING_JUMP = 10;
            ConstantPelt.PENALTY_BETA = 0.5;
            ConstantPelt.MINIMUM_SEGMENT_DURATION_SECONDS = 1.2;


            ConstantAnomalyDetection.MINIMUM_DURATION_SECONDS = 0.5;
            
            ConstantAnomalyDetection.MINIMUM_RANGEZ = 1.2;
            ConstantAnomalyDetection.PATTERN_SUPPORT_THRESHOLD = 4;

            ConstantAnomalyDetection.FINAL_SCORE = 0.9;
            ConstantAnomalyDetection.HASH_SIMILARITY = 0.55;
            ConstantAnomalyDetection.FEATURE_SIMILARITY = 0.2;
            ConstantAnomalyDetection.DURATION_SIMILARITY = 0.05;

            ConstantAnomalyDetection.HASH_THRESHOLD = 0.015;
            ConstantAnomalyDetection.RARE_LABEL_COUNT_MAX = 4;
            ConstantAnomalyDetection.RARE_LABEL_TIME_FRACTION = 0.1;
            ConstantAnomalyDetection.POST_MINIMUM_GAP_SECONDS = 10;


        }
        private int PickRepresentativePoint(
    List<double> processedSignal,
    SegmentBoundary segment,
    string label)
        {
            int start = segment.StartIndex;
            int end = segment.EndIndex;

            double[] segmentValues = processedSignal
                                     .Skip(start)
                                     .Take(end - start)
                                     .ToArray();

            if (label == "RampDown" || label == "SpikeLow" || label == "BelowBound")
            {
                int localIndex = Array.IndexOf(segmentValues, segmentValues.Min());
                return start + localIndex;
            }

            if (label == "RampUp" || label == "SpikeHigh" || label == "AboveBound")
            {
                int localIndex = Array.IndexOf(segmentValues, segmentValues.Max());
                return start + localIndex;
            }

            if (label == "Oscillation")
            {
                double maxAbs = segmentValues.Max(v => Math.Abs(v));
                int localIndex = Array.IndexOf(segmentValues, maxAbs);
                return start + localIndex;
            }

            double absMax = segmentValues.Max(v => Math.Abs(v));
            int idxAbs = Array.IndexOf(segmentValues, absMax);
            return start + idxAbs;
        }

    }
}
