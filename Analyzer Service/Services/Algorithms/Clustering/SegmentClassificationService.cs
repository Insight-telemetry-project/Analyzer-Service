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

        private async Task<List<double>> LoadSignalValuesAsync(int masterIndex, string fieldName)
        {
            List<double> signalValues = await flightDataPreparer.GetParameterValuesCopyAsync(masterIndex, fieldName);
            return signalValues;
        }

        private static List<double> BuildSyntheticTimeSeriesValues(int sampleCount)
        {
            List<double> timeSeriesValues = new List<double>(sampleCount);

            for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                timeSeriesValues.Add(sampleIndex);
            }

            return timeSeriesValues;
        }

        private async Task<List<SegmentBoundary>> DetectSegments(
            int masterIndex,
            string fieldName,
            int totalSampleCount,
            flightStatus status)
        {
            List<int> rawChangePointIndexes =
                await changePointDetectionService.DetectChangePointsAsync(masterIndex, fieldName, status);

            List<int> cleanedChangePointIndexes =
                rawChangePointIndexes
                    .Distinct()
                    .Where(changePointIndex => changePointIndex > 0 && changePointIndex < totalSampleCount)
                    .OrderBy(changePointIndex => changePointIndex)
                    .ToList();

            if (!cleanedChangePointIndexes.Contains(totalSampleCount))
            {
                cleanedChangePointIndexes.Add(totalSampleCount);
            }

            List<SegmentBoundary> segments =
                featureExtractionUtility.BuildSegmentsFromPoints(cleanedChangePointIndexes, totalSampleCount);

            return segments;
        }

        private List<double> PreprocessSignal(List<double> signalValues)
        {
            if (!IsNoisy)
            {
                double[] filteredSignal =
                    signalProcessingUtility.ApplyHampel(
                        signalValues.ToArray(),
                        ConstantAlgorithm.HAMPEL_WINDOW,
                        ConstantAlgorithm.HAMPEL_SIGMA);

                List<double> normalizedSignal = signalProcessingUtility.ApplyZScore(filteredSignal);
                return normalizedSignal;
            }

            double[] signalArray = signalValues.ToArray();
            List<double> normalizedSignalValues = signalProcessingUtility.ApplyZScore(signalArray);
            return normalizedSignalValues;
        }

        public async Task<SegmentAnalysisResult> ClassifyWithAnomaliesAsync(
            int masterIndex,
            string fieldName,
            int startIndex,
            int endIndex,
            flightStatus status)
        {
            List<double> rawSignalValues = await LoadSignalValuesAsync(masterIndex, fieldName);

            List<double> timeSeriesValues = BuildSyntheticTimeSeriesValues(rawSignalValues.Count);

            bool isNoisyFlight = DetermineIsNoisyFlight(rawSignalValues);
            IsNoisy = isNoisyFlight;

            signalNoiseTuning.ApplyHighNoiseConfiguration();

            List<SegmentBoundary> detectedSegments =
                await DetectSegments(masterIndex, fieldName, rawSignalValues.Count, status);

            List<double> processedSignalValues = PreprocessSignal(rawSignalValues);

            List<SegmentClassificationResult> segmentClassificationResults =
                BuildMergedSegmentResults(timeSeriesValues, processedSignalValues, detectedSegments);

            List<SegmentBoundary> mergedSegmentBoundaries =
                segmentClassificationResults
                    .Select(result => result.Segment)
                    .ToList();

            List<SegmentFeatures> featureList =
                BuildFeatureList(timeSeriesValues, processedSignalValues, mergedSegmentBoundaries);

            AttachFeaturesToResults(segmentClassificationResults, featureList);
            AttachHashVectors(timeSeriesValues, processedSignalValues, segmentClassificationResults);

            List<int> detectedAnomalySegmentIndexes =
                DetectAnomalies(
                    timeSeriesValues,
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

            await StoreAnomaliesAsync(
                masterIndex,
                fieldName,
                timeSeriesValues,
                processedSignalValues,
                mergedSegmentBoundaries,
                segmentClassificationResults,
                detectedAnomalySegmentIndexes,
                featureList);

            SegmentAnalysisResult result = new SegmentAnalysisResult
            {
                Segments = segmentClassificationResults,
                SegmentBoundaries = mergedSegmentBoundaries,
                AnomalyIndexes = anomalySampleIndexes
            };

            return result;
        }

        private List<int> DetectAnomalies(
            List<double> timeSeriesValues,
            List<double> processedSignalValues,
            List<SegmentBoundary> segmentBoundaries,
            List<SegmentClassificationResult> classificationResults,
            List<SegmentFeatures> featureList,
            flightStatus status)
        {
            List<int> detectedSegmentIndexes = anomalyDetectionUtility.DetectAnomalies(
                processedSignalValues,
                segmentBoundaries,
                classificationResults.Select(result => result.Label).ToList(),
                featureList,
                status);

            if (status == flightStatus.TakeOf_Landing)
            {
                return detectedSegmentIndexes.Take(10).ToList();
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
            List<double> processedSignalValues,
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
            List<double> timeSeriesValues,
            List<double> processedSignalValues,
            List<SegmentBoundary> detectedSegments)
        {
            List<double> meanValues =
                segmentLogicUtility.ComputeMeansPerSegment(processedSignalValues, detectedSegments);
            
            List<SegmentClassificationResult> classificationResults =
                segmentLogicUtility.ClassifySegments(
                    timeSeriesValues,
                    processedSignalValues,
                    detectedSegments,
                    meanValues);

            return classificationResults;
        }

        private List<SegmentFeatures> BuildFeatureList(
            List<double> timeSeriesValues,
            List<double> processedSignalValues,
            List<SegmentBoundary> segmentBoundaries)
        {
            List<double> meanValues =
                segmentLogicUtility.ComputeMeansPerSegment(processedSignalValues, segmentBoundaries);

            List<SegmentFeatures> featureList =
                segmentLogicUtility.BuildFeatureList(
                    timeSeriesValues,
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

        private void AttachHashVectors(
            List<double> timeSeriesValues,
            List<double> processedSignalValues,
            List<SegmentClassificationResult> results)
        {
            for (int resultIndex = 0; resultIndex < results.Count; resultIndex++)
            {
                SegmentBoundary segmentBoundary = results[resultIndex].Segment;

                string hashString =patternHashingUtility.ComputeHash(processedSignalValues,segmentBoundary);


                double[] hashVector =
                    hashString
                        .Split(',')
                        .Select(double.Parse)
                        .ToArray();

                results[resultIndex].HashVector = hashVector;
            }
        }

        private async Task StoreAnomaliesAsync(
            int masterIndex,
            string fieldName,
            List<double> timeSeriesValues,
            List<double> processedSignalValues,
            List<SegmentBoundary> segmentBoundaries,
            List<SegmentClassificationResult> classificationResults,
            List<int> anomalySegmentIndexes,
            List<SegmentFeatures> featureList)
        {
            for (int anomalyIndex = 0; anomalyIndex < anomalySegmentIndexes.Count; anomalyIndex++)
            {
                int segmentIndex = anomalySegmentIndexes[anomalyIndex];

                await StoreSingleAnomalyAsync(
                    masterIndex,
                    fieldName,
                    timeSeriesValues,
                    processedSignalValues,
                    segmentBoundaries,
                    classificationResults,
                    featureList,
                    segmentIndex);
            }
        }

        private async Task StoreSingleAnomalyAsync(
            int masterIndex,
            string fieldName,
            List<double> timeSeriesValues,
            List<double> processedSignalValues,
            List<SegmentBoundary> segmentBoundaries,
            List<SegmentClassificationResult> classificationResults,
            List<SegmentFeatures> featureList,
            int segmentIndex)
        {
            SegmentBoundary segmentBoundary = segmentBoundaries[segmentIndex];
            string segmentLabel = classificationResults[segmentIndex].Label;

            int representativeSampleIndex =
                signalNoiseTuning.SelectRepresentativeSampleIndex(
                    processedSignalValues,
                    segmentBoundary,
                    segmentLabel);

            double timestamp = timeSeriesValues[representativeSampleIndex];

            await flightTelemetryMongoProxy.StoreAnomalyAsync(
                masterIndex,
                fieldName,
                timestamp);

            SegmentFeatures segmentFeatures = featureList[segmentIndex];

            string patternHash =patternHashingUtility.ComputeHash(processedSignalValues,segmentBoundary);

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

        private bool DetermineIsNoisyFlight(List<double> signalValues)
        {
            List<double> diffs = new List<double>(signalValues.Count - 1);

            for (int sampleIndex = 1; sampleIndex < signalValues.Count; sampleIndex++)
            {
                double currentValue = signalValues[sampleIndex];
                double previousValue = signalValues[sampleIndex - 1];
                diffs.Add(Math.Abs(currentValue - previousValue));
            }

            diffs.Sort();

            int count = diffs.Count;

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
