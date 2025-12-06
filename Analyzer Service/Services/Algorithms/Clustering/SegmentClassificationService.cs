using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms.AnomalyDetector;
using Analyzer_Service.Models.Interface.Algorithms.Clustering;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Algorithms.Random_Forest;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Models.Schema;


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

        public SegmentClassificationService(
            IPrepareFlightData flightDataPreparer,
            IChangePointDetectionService changePointDetectionService,
            ISignalProcessingUtility signalProcessingUtility,
            IFeatureExtractionUtility featureExtractionUtility,
            IAnomalyDetectionUtility anomalyDetectionUtility,
            ISegmentLogicUtility segmentLogicUtility,
            IFlightTelemetryMongoProxy flightTelemetryMongoProxy,
            IPatternHashingUtility patternHashingUtility,
            ISignalNoiseTuning signalNoiseTuning)
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
        }

        private async Task<SignalSeries> LoadFlightData(int masterIndex, string fieldName)
        {
            return await flightDataPreparer.PrepareFlightDataAsync(masterIndex, ConstantFligth.TIMESTEP_COL, fieldName);
        }

        private async Task<List<SegmentBoundary>> DetectSegments(int masterIndex, string fieldName, int totalSampleCount)
        {
            List<int> rawChangePointIndexes = await changePointDetectionService.DetectChangePointsAsync(masterIndex, fieldName);

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

            return featureExtractionUtility.BuildSegmentsFromPoints(cleanedChangePointIndexes, totalSampleCount);
        }

        private List<double> PreprocessSignal(List<double> signalValues)
        {
            double[] signalArray = signalValues.ToArray();
            List<double> normalizedSignal = signalProcessingUtility.ApplyZScore(signalArray);
            return normalizedSignal;
        }

        public async Task<SegmentAnalysisResult> ClassifyWithAnomaliesAsync(int masterIndex, string fieldName, int startIndex, int endIndex)
        {
            signalNoiseTuning.ApplyConstantPeltConfiguration();

            (List<double> timeSeriesValues, List<double> signalValues) =
                await LoadRangeAsync(masterIndex, fieldName, startIndex, endIndex);

            List<SegmentBoundary> detectedSegments =
                await DetectSegments(masterIndex, fieldName, signalValues.Count);

            List<double> processedSignalValues = PreprocessSignal(signalValues);

            List<SegmentClassificationResult> segmentClassificationResults =
                BuildMergedSegmentResults(timeSeriesValues, processedSignalValues, detectedSegments);

            List<SegmentBoundary> mergedSegmentBoundaries =
                segmentClassificationResults.Select(result => result.Segment).ToList();

            List<Dictionary<string, double>> featureList =
                BuildFeatureList(timeSeriesValues, processedSignalValues, mergedSegmentBoundaries);

            AttachFeaturesToResults(segmentClassificationResults, featureList);
            AttachHashVectors(timeSeriesValues, processedSignalValues, segmentClassificationResults);

            List<int> detectedAnomalySegmentIndexes =
                DetectAndFilterAnomalies(timeSeriesValues, processedSignalValues, mergedSegmentBoundaries, segmentClassificationResults, featureList);

            List<int> anomalySampleIndexes =
                ComputeRepresentativeSamples(processedSignalValues, mergedSegmentBoundaries, segmentClassificationResults, detectedAnomalySegmentIndexes);

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

        private List<int> DetectAndFilterAnomalies(
            List<double> timeSeriesValues,
            List<double> processedSignalValues,
            List<SegmentBoundary> segmentBoundaries,
            List<SegmentClassificationResult> classificationResults,
            List<Dictionary<string, double>> featureList)
        {
            List<int> detected = anomalyDetectionUtility.DetectAnomalies(
                timeSeriesValues,
                processedSignalValues,
                segmentBoundaries,
                classificationResults.Select(result => result.Label).ToList(),
                featureList);

            List<int> filtered =
                detected.Where(index =>
                    featureList[index][ConstantRandomForest.RANGE_Z_JSON] >= ConstantAnomalyDetection.MIN_SIGNIFICANT_RANGE_Z).ToList();

            List<(int Index, double Score)> ranked =
                filtered.Select(index => (
                    Index: index,
                    Score: signalNoiseTuning.ComputeAnomalyStrengthScore(featureList[index])
                ))
                .OrderByDescending(item => item.Score)
                .ToList();

            List<int> finalSelection =
                ranked.Take(ConstantAnomalyDetection.MAX_ANOMALIES_PER_FLIGHT)
                      .Select(item => item.Index)
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

            for (int i = 0; i < selectedSegmentIndexes.Count; i++)
            {
                int segmentIndex = selectedSegmentIndexes[i];
                SegmentBoundary segment = segmentBoundaries[segmentIndex];
                string label = classificationResults[segmentIndex].Label;

                int representative =
                    signalNoiseTuning.SelectRepresentativeSampleIndex(processedSignalValues, segment, label);

                representativeSamples.Add(representative);
            }

            return representativeSamples;
        }

        private async Task<(List<double> Time, List<double> Signal)> LoadRangeAsync(int masterIndex, string fieldName, int startIndex, int endIndex)
        {
            SignalSeries fullSeries = await LoadFlightData(masterIndex, fieldName);

            List<double> fullTimeSeries = fullSeries.Time;
            List<double> fullSignalSeries = fullSeries.Values;

            if (!(startIndex == 0 && endIndex == 0))
            {
                if (startIndex < 0) startIndex = 0;
                if (endIndex > fullSignalSeries.Count - 1) endIndex = fullSignalSeries.Count - 1;
                if (startIndex >= endIndex) return (new List<double>(), new List<double>());

                fullTimeSeries = fullTimeSeries.Skip(startIndex).Take(endIndex - startIndex + 1).ToList();
                fullSignalSeries = fullSignalSeries.Skip(startIndex).Take(endIndex - startIndex + 1).ToList();
            }

            return (fullTimeSeries, fullSignalSeries);
        }

        private List<SegmentClassificationResult> BuildMergedSegmentResults(
            List<double> timeSeriesValues,
            List<double> processedSignalValues,
            List<SegmentBoundary> detectedSegments)
        {
            List<double> meanValues =
                segmentLogicUtility.ComputeMeansPerSegment(processedSignalValues, detectedSegments);

            return segmentLogicUtility.ClassifySegments(timeSeriesValues, processedSignalValues, detectedSegments, meanValues);
        }

        private List<Dictionary<string, double>> BuildFeatureList(
            List<double> timeSeriesValues,
            List<double> processedSignalValues,
            List<SegmentBoundary> segmentBoundaries)
        {
            List<double> meanValues =
                segmentLogicUtility.ComputeMeansPerSegment(processedSignalValues, segmentBoundaries);

            return segmentLogicUtility.BuildFeatureList(timeSeriesValues, processedSignalValues, segmentBoundaries, meanValues);
        }

        private void AttachFeaturesToResults(
            List<SegmentClassificationResult> classificationResults,
            List<Dictionary<string, double>> featureList)
        {
            for (int index = 0; index < classificationResults.Count; index++)
            {
                classificationResults[index].FeatureValues = featureList[index];
            }
        }

        private void AttachHashVectors(
            List<double> timeSeriesValues,
            List<double> processedSignalValues,
            List<SegmentClassificationResult> results)
        {
            for (int index = 0; index < results.Count; index++)
            {
                string hashString = patternHashingUtility.ComputeHash(
                    timeSeriesValues,
                    processedSignalValues,
                    results[index].Segment);

                double[] hashVector =
                    hashString.Split(',').Select(double.Parse).ToArray();

                results[index].HashVector = hashVector;
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
            List<Dictionary<string, double>> featureList)
        {
            for (int indexSegment = 0; indexSegment < anomalySegmentIndexes.Count; indexSegment++)
            {
                int segmentIndex = anomalySegmentIndexes[indexSegment];
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
            List<Dictionary<string, double>> featureList,
            int segmentIndex)
        {
            SegmentBoundary segmentBoundary = segmentBoundaries[segmentIndex];
            string segmentLabel = classificationResults[segmentIndex].Label;

            int representativeSampleIndex =
                signalNoiseTuning.SelectRepresentativeSampleIndex(processedSignalValues, segmentBoundary, segmentLabel);

            double timestamp = timeSeriesValues[representativeSampleIndex];

            await flightTelemetryMongoProxy.StoreAnomalyAsync(masterIndex, fieldName, timestamp);

            Dictionary<string, double> segmentFeatures = featureList[segmentIndex];

            string patternHash =
                patternHashingUtility.ComputeHash(timeSeriesValues, processedSignalValues, segmentBoundary);

            HistoricalAnomalyRecord record = new HistoricalAnomalyRecord
            {
                MasterIndex = masterIndex,
                ParameterName = fieldName,
                StartIndex = segmentBoundary.StartIndex,
                EndIndex = segmentBoundary.EndIndex,
                Label = segmentLabel,
                PatternHash = patternHash,
                FeatureValues = segmentFeatures,
                CreatedAt = System.DateTime.UtcNow
            };

            await flightTelemetryMongoProxy.StoreHistoricalAnomalyAsync(record);
        }
    }
}
