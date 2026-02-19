using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Enums;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms.AnomalyDetector;
using Analyzer_Service.Models.Interface.Algorithms.Clustering;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Algorithms.Random_Forest;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Models.Schema;
using System;
using System.Buffers;
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
        private readonly ISignalNoiseTuning signalNoiseTuning;
        private readonly ITuningSettingsFactory tuningSettingsFactory;

        public bool IsNoisy = true;

        public SegmentClassificationService(
            IPrepareFlightData flightDataPreparer,
            IChangePointDetectionService changePointDetectionService,
            ISignalProcessingUtility signalProcessingUtility,
            IFeatureExtractionUtility featureExtractionUtility,
            IAnomalyDetectionUtility anomalyDetectionUtility,
            ISegmentLogicUtility segmentLogicUtility,
            IFlightTelemetryMongoProxy flightTelemetryMongoProxy,
            IPatternHashingUtility patternHashingUtility,
            ISignalNoiseTuning signalNoiseTuning,
            ITuningSettingsFactory tuningSettingsFactory)
        {
            this.flightDataPreparer = flightDataPreparer;
            this.changePointDetectionService = changePointDetectionService;
            this.signalProcessingUtility = signalProcessingUtility;
            this.featureExtractionUtility = featureExtractionUtility;
            this.anomalyDetectionUtility = anomalyDetectionUtility;
            this.segmentLogicUtility = segmentLogicUtility;
            this.flightTelemetryMongoProxy = flightTelemetryMongoProxy;
            this.patternHashingUtility = patternHashingUtility;
            this.signalNoiseTuning = signalNoiseTuning;
            this.tuningSettingsFactory = tuningSettingsFactory;
        }

        private async Task<double[]> LoadSignalValuesAsync(int masterIndex, string fieldName)
        {
            double[] values =
                await flightDataPreparer.GetParameterValuesAsync(masterIndex, fieldName);

            return values;
        }






        private List<SegmentBoundary> DetectSegments(
            double[] rawSignalValues,flightStatus status)
        {
            List<int> rawChangePointIndexes =
                changePointDetectionService.DetectChangePoints(rawSignalValues, status);

            List<int> cleanedChangePointIndexes =
                rawChangePointIndexes
                    .Distinct()
                    .Where(index => index > 0 && index < rawSignalValues.Length)
                    .OrderBy(index => index)
                    .ToList();

            if (!cleanedChangePointIndexes.Contains(rawSignalValues.Length))
            {
                cleanedChangePointIndexes.Add(rawSignalValues.Length);
            }

            List<SegmentBoundary> segments =
                featureExtractionUtility.BuildSegmentsFromPoints(
                    cleanedChangePointIndexes,
                    rawSignalValues.Length);

            return segments;
        }





        public async Task<SegmentAnalysisResult> ClassifyWithAnomaliesAsync(
            int masterIndex,string fieldName,
            int startIndex,int endIndex,flightStatus status)
        {
            double[] rawSignalValues = await LoadSignalValuesAsync(masterIndex, fieldName);

            bool isNoisyFlight = DetermineIsNoisyFlight(rawSignalValues);
            IsNoisy = isNoisyFlight;

            signalNoiseTuning.ApplyHighNoiseConfiguration();

            List<SegmentBoundary> detectedSegments =
                DetectSegments(rawSignalValues,status);


            //int processedLength;
            double[] processedSignalValues =
                signalProcessingUtility.ApplyZScore(
                    rawSignalValues
                    //,out processedLength
                    );

            List<SegmentClassificationResult> segmentClassificationResults =
                BuildMergedSegmentResults(
                    processedSignalValues,
                    detectedSegments);

            List<SegmentBoundary> mergedSegmentBoundaries =
                segmentClassificationResults
                    .Select(result => result.Segment)
                    .ToList();

            List<SegmentFeatures> featureList =
                BuildFeatureList(
                    processedSignalValues,
                    mergedSegmentBoundaries);

            AttachFeaturesToResults(
                segmentClassificationResults,
                featureList);

            AttachHashVectors(
                processedSignalValues,
                segmentClassificationResults);

            List<int> detectedAnomalySegmentIndexes =
                DetectAnomalies(
                    processedSignalValues,
                    mergedSegmentBoundaries,
                    segmentClassificationResults,
                    featureList,
                    status);

            List<int> anomalySampleIndexes =
                ComputeRepresentativeSamples(
                    processedSignalValues,
                    mergedSegmentBoundaries,
                    segmentClassificationResults,
                    detectedAnomalySegmentIndexes);

            long flightStartEpochSeconds =await flightDataPreparer.GetFlightStartEpochSecondsAsync(masterIndex);

            await StoreAnomaliesAsync(
                masterIndex,fieldName,processedSignalValues,mergedSegmentBoundaries,
                segmentClassificationResults,detectedAnomalySegmentIndexes,featureList,flightStartEpochSeconds);


            SegmentAnalysisResult result = new SegmentAnalysisResult
            {
                Segments = segmentClassificationResults,
                SegmentBoundaries = mergedSegmentBoundaries,
                AnomalyIndexes = anomalySampleIndexes
            };

            return result;
        }



        private List<int> DetectAnomalies(
            double[] processedSignalValues,
            List<SegmentBoundary> segmentBoundaries,
            List<SegmentClassificationResult> classificationResults,
            List<SegmentFeatures> featureList,
            flightStatus status)
        {
            List<int> detectedSegmentIndexes = anomalyDetectionUtility.DetectAnomalies(
                processedSignalValues,segmentBoundaries,classificationResults,
                featureList,status);


            if (status == flightStatus.TakeOf_Landing)
            {
                //return detectedSegmentIndexes.Take(10).ToList();
                return detectedSegmentIndexes.ToList();

            }

            if (!IsNoisy)
            {
                return detectedSegmentIndexes;
            }

            List<int> filteredSegmentIndexes =
                detectedSegmentIndexes
                    .Where(segmentIndex => featureList[segmentIndex].RangeZ >= ConstantAnomalyDetection.MIN_SIGNIFICANT_RANGE_Z)
                    .ToList();

            List<(int SegmentIndex, double Score)> rankedSegmentIndexes =
                filteredSegmentIndexes
                    .Select(segmentIndex => (
                        SegmentIndex: segmentIndex,
                        Score: signalNoiseTuning.ComputeAnomalyStrengthScore(featureList[segmentIndex])
                    ))
                    .OrderByDescending(item => item.Score)
                    .ToList();

            List<int> finalSelection =
                rankedSegmentIndexes
                    .Take(ConstantAnomalyDetection.MAX_ANOMALIES_PER_FLIGHT)
                    .Select(item => item.SegmentIndex)
                    .ToList();

            return finalSelection;
        }

        private List<int> ComputeRepresentativeSamples(
            double[] processedSignalValues,
            List<SegmentBoundary> segmentBoundaries,
            List<SegmentClassificationResult> classificationResults,
            List<int> selectedSegmentIndexes)
        {
            List<int> representativeSamples = new List<int>();

            for (int selectedIndex = 0; selectedIndex < selectedSegmentIndexes.Count; selectedIndex++)
            {
                int segmentIndex = selectedSegmentIndexes[selectedIndex];
                SegmentBoundary segmentBoundary = segmentBoundaries[segmentIndex];
                string segmentLabel = classificationResults[segmentIndex].Label;

                int representativeSampleIndex =
                    signalNoiseTuning.SelectRepresentativeSampleIndex(
                        processedSignalValues,
                        segmentBoundary,
                        segmentLabel);

                representativeSamples.Add(representativeSampleIndex);
            }

            return representativeSamples;
        }

        private List<SegmentClassificationResult> BuildMergedSegmentResults(
            double[] processedSignalValues,List<SegmentBoundary> detectedSegments)
        {
            double[] meanValues = segmentLogicUtility.ComputeMeansPerSegment(processedSignalValues, detectedSegments);

            List<SegmentClassificationResult> classificationResults =
                segmentLogicUtility.ClassifySegments(
                    processedSignalValues,
                    detectedSegments,
                    meanValues);

            return classificationResults;
        }


        private List<SegmentFeatures> BuildFeatureList(
            double[] processedSignalValues,List<SegmentBoundary> segmentBoundaries)
        {
            double[] meanValues =segmentLogicUtility.ComputeMeansPerSegment(processedSignalValues, segmentBoundaries);

            List<SegmentFeatures> featureList =
                segmentLogicUtility.BuildFeatureList(
                    processedSignalValues,
                    segmentBoundaries,
                    meanValues);

            return featureList;
        }


        private void AttachFeaturesToResults(
            List<SegmentClassificationResult> classificationResults,
            List<SegmentFeatures> featureList)
        {
            for (int resultIndex = 0; resultIndex < classificationResults.Count; resultIndex++)
            {
                classificationResults[resultIndex].FeatureValues = featureList[resultIndex];
            }
        }

        private void AttachHashVectors(double[] processedSignalValues,List<SegmentClassificationResult> results)
        {
            for (int resultIndex = 0; resultIndex < results.Count; resultIndex++)
            {
                SegmentBoundary segmentBoundary = results[resultIndex].Segment;

                double[] hashVector =
                    patternHashingUtility.ComputeHashVector(processedSignalValues, segmentBoundary);

                results[resultIndex].HashVector = hashVector;
            }
        }


        private async Task StoreAnomaliesAsync(
            int masterIndex,string fieldName,double[] processedSignalValues,
            List<SegmentBoundary> segmentBoundaries,List<SegmentClassificationResult> classificationResults,
            List<int> anomalySegmentIndexes,List<SegmentFeatures> featureList,long flightStartEpochSeconds)
        {
            for (int anomalyIndex = 0; anomalyIndex < anomalySegmentIndexes.Count; anomalyIndex++)
            {
                int segmentIndex = anomalySegmentIndexes[anomalyIndex];

                await StoreSingleAnomalyAsync(
                    masterIndex,
                    fieldName,
                    processedSignalValues,
                    segmentBoundaries,
                    classificationResults,
                    featureList,
                    segmentIndex,
                    flightStartEpochSeconds);
            }
        }


        private async Task StoreSingleAnomalyAsync(
            int masterIndex,string fieldName,double[] processedSignalValues,
            List<SegmentBoundary> segmentBoundaries,List<SegmentClassificationResult> classificationResults,
            List<SegmentFeatures> featureList,int segmentIndex,long flightStartEpochSeconds)
        {
            SegmentBoundary segmentBoundary = segmentBoundaries[segmentIndex];
            string segmentLabel = classificationResults[segmentIndex].Label;

            int representativeSampleIndex =
                signalNoiseTuning.SelectRepresentativeSampleIndex(
                    processedSignalValues,
                    segmentBoundary,
                    segmentLabel);

            long anomalyEpochSeconds = flightStartEpochSeconds + (long)representativeSampleIndex;

            await flightTelemetryMongoProxy.StoreAnomalyAsync(
                masterIndex,
                fieldName,
                anomalyEpochSeconds);

            SegmentFeatures segmentFeatures = featureList[segmentIndex];

            string patternHash = patternHashingUtility.ComputeHash(processedSignalValues, segmentBoundary);

            HistoricalAnomalyRecord record = new HistoricalAnomalyRecord
            {
                MasterIndex = masterIndex,
                ParameterName = fieldName,
                StartIndex = segmentBoundary.StartIndex,
                EndIndex = segmentBoundary.EndIndex,
                Label = segmentLabel,
                PatternHash = patternHash,
                FeatureValues = segmentFeatures,
                CreatedAt = DateTime.UtcNow
            };

            await flightTelemetryMongoProxy.StoreHistoricalAnomalyAsync(record);
        }


        private bool DetermineIsNoisyFlight(double[] signalValues)
        {
            if (signalValues.Length < 2)
            {
                return false;
            }

            double[] diffs = new double[signalValues.Length - 1];

            for (int sampleIndex = 1; sampleIndex < signalValues.Length; sampleIndex++)
            {
                double currentValue = signalValues[sampleIndex];
                double previousValue = signalValues[sampleIndex - 1];
                diffs[sampleIndex - 1] = Math.Abs(currentValue - previousValue);
            }

            Array.Sort(diffs);

            int count = diffs.Length;

            double medianDiff = diffs[count / 2];
            double safeMedian = Math.Max(medianDiff, ConstantAlgorithm.NOT_DIVIDE_IN_ZERO);

            int p95Index = (int)Math.Floor(ConstantPelt.P95Quantile * (count - 1));
            double p95Diff = diffs[p95Index];

            double spikeThreshold = safeMedian * ConstantPelt.SPIKE_THRESHOLD_MEDIAN_MULTIPLIER;

            int spikeCount = 0;
            for (int diffIndex = 0; diffIndex < count; diffIndex++)
            {
                if (diffs[diffIndex] >= spikeThreshold)
                {
                    spikeCount++;
                }
            }

            double spikeFraction = (double)spikeCount / (double)count;
            double tailRatio = p95Diff / safeMedian;

            bool hasTooManyLargeSpikes = spikeFraction >= ConstantPelt.MAX_ALLOWED_SPIKE_FRACTION;
            bool hasHeavyTailComparedToTypical = tailRatio >= ConstantPelt.MAX_ALLOWED_TAIL_RATIO;

            bool isNoisy = hasTooManyLargeSpikes || hasHeavyTailComparedToTypical;
            return isNoisy;
        }
    }
}
