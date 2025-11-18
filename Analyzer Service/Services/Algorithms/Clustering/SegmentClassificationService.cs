using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms.AnomalyDetector;
using Analyzer_Service.Models.Interface.Algorithms.Clustering;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Algorithms.Random_Forest;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Services.Algorithms;
using Analyzer_Service.Services.Algorithms.Random_Forest;
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
        private readonly IRandomForestModelProvider modelProvider;
        private readonly IRandomForestOperations randomForestOperations;

        public SegmentClassificationService(
    IPrepareFlightData flightDataPreparer,
    IChangePointDetectionService changePointDetectionService,
    ISignalProcessingUtility signalProcessingUtility,
    IFeatureExtractionUtility featureExtractionUtility,
    IAnomalyDetectionUtility anomalyDetectionUtility,
    IRandomForestModelProvider modelProvider,
    IRandomForestOperations randomForestOperations)
        {
            this.flightDataPreparer = flightDataPreparer;
            this.changePointDetectionService = changePointDetectionService;
            this.signalProcessingUtility = signalProcessingUtility;
            this.featureExtractionUtility = featureExtractionUtility;
            this.anomalyDetectionUtility = anomalyDetectionUtility;
            this.modelProvider = modelProvider;
            this.randomForestOperations = randomForestOperations;
        }


        public async Task<List<SegmentClassificationResult>> ClassifyAsync(int masterIndex, string fieldName)
        {
            (List<double> timeSeries, List<double> signalSeries) =
                await LoadFlightData(masterIndex, fieldName);

            List<SegmentBoundary> detectedSegments =
                await DetectSegments(masterIndex, fieldName, signalSeries.Count);

            List<double> normalizedSignal = PreprocessSignal(signalSeries);

            List<double> meanValues = ComputeMeansPerSegment(normalizedSignal, detectedSegments);

            return ClassifySegments(timeSeries, normalizedSignal, detectedSegments, meanValues);
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
                    .Where(b => b > 0 && b < sampleCount)
                    .OrderBy(b => b)
                    .ToList();

            if (!cleaned.Contains(sampleCount))
                cleaned.Add(sampleCount);

            return featureExtractionUtility.BuildSegmentsFromPoints(cleaned, sampleCount);
        }

        private List<double> PreprocessSignal(List<double> signal)
        {
            double[] filtered =
                signalProcessingUtility.ApplyHampel(signal.ToArray(),
                ConstantAlgorithm.HAMPEL_WINDOW,
                ConstantAlgorithm.HAMPEL_SIGMA);

            return signalProcessingUtility.ApplyZScore(filtered);
        }

        private List<double> ComputeMeansPerSegment(List<double> signal, List<SegmentBoundary> segments)
        {
            List<double> means = new List<double>(segments.Count);

            foreach (var seg in segments)
                means.Add(signalProcessingUtility.ComputeMean(signal, seg.StartIndex, seg.EndIndex));

            return means;
        }

        private List<SegmentClassificationResult> ClassifySegments(
            List<double> timeSeries,
            List<double> signal,
            List<SegmentBoundary> segments,
            List<double> meanValues)
        {
            List<SegmentClassificationResult> list = new List<SegmentClassificationResult>();

            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                double prev = i > 0 ? meanValues[i - 1] : 0.0;
                double next = i < meanValues.Count - 1 ? meanValues[i + 1] : 0.0;

                double[] features = featureExtractionUtility.ExtractFeatures(
                    timeSeries, signal, seg, prev, next);

                string label = randomForestOperations.PredictLabel(modelProvider, features);


                list.Add(new SegmentClassificationResult(seg, label));
            }

            return MergeSegments(list);
        }

        private List<SegmentClassificationResult> MergeSegments(List<SegmentClassificationResult> segments)
        {
            List<SegmentClassificationResult> merged = new List<SegmentClassificationResult>();

            SegmentClassificationResult current = segments[0];

            for (int i = 1; i < segments.Count; i++)
            {
                var next = segments[i];

                if (current.Label == next.Label &&
                    current.Segment.EndIndex == next.Segment.StartIndex)
                {
                    current = new SegmentClassificationResult(
                        new SegmentBoundary(current.Segment.StartIndex, next.Segment.EndIndex),
                        current.Label);
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


        public async Task<(List<SegmentClassificationResult> Segments, List<int> Anomalies)>
            ClassifyWithAnomaliesAsync(int masterIndex, string fieldName)
        {
            (List<double> timeSeries, List<double> signalSeries) =
                await LoadFlightData(masterIndex, fieldName);

            List<SegmentBoundary> rawSegments =
                await DetectSegments(masterIndex, fieldName, signalSeries.Count);

            List<double> processed = PreprocessSignal(signalSeries);

            List<double> means = ComputeMeansPerSegment(processed, rawSegments);

            List<SegmentClassificationResult> merged =
                ClassifySegments(timeSeries, processed, rawSegments, means);

            List<SegmentBoundary> segs =
                merged.Select(m => m.Segment).ToList();

            List<double> mergedMeans =
                ComputeMeansPerSegment(processed, segs);

            List<Dictionary<string, double>> featureList =
                BuildFeatureList(timeSeries, processed, segs, mergedMeans);

            List<string> labels =
                merged.Select(m => m.Label).ToList();

            List<int> anomalies =
                anomalyDetectionUtility.DetectAnomalies(
                    timeSeries, processed, segs, labels, featureList);

            return (merged, anomalies);
        }

        private List<Dictionary<string, double>> BuildFeatureList(
            List<double> timeSeries,
            List<double> signal,
            List<SegmentBoundary> segments,
            List<double> meanValues)
        {
            List<Dictionary<string, double>> list =
                new List<Dictionary<string, double>>(segments.Count);

            for (int i = 0; i < segments.Count; i++)
            {
                double prev = i > 0 ? meanValues[i - 1] : 0.0;
                double next = i < meanValues.Count - 1 ? meanValues[i + 1] : 0.0;

                double[] vec =
                    featureExtractionUtility.ExtractFeatures(
                        timeSeries, signal, segments[i], prev, next);

                list.Add(modelProvider.BuildFeatureDictionary(vec));
            }

            return list;
        }
    }
}
