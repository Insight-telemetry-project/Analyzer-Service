using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Enums;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Algorithms.Random_Forest;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Models.Schema;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Analyzer_Service.Services.Algorithms.Pelt
{
    public class FlightPhaseAnalysisService: IFlightPhaseAnalysisService
    {
        private readonly IPrepareFlightData flightDataPreparer;
        private readonly IChangePointDetectionService changePointDetectionService;
        private readonly ISignalProcessingUtility signalProcessingUtility;
        private readonly IFeatureExtractionUtility featureExtractionUtility;
        private readonly ISegmentLogicUtility segmentLogicUtility;
        private readonly IFlightPhaseDetector flightPhaseDetector;

        public FlightPhaseAnalysisService(
            IPrepareFlightData flightDataPreparer,
            IChangePointDetectionService changePointDetectionService,
            ISignalProcessingUtility signalProcessingUtility,
            IFeatureExtractionUtility featureExtractionUtility,
            ISegmentLogicUtility segmentLogicUtility,
            IFlightPhaseDetector flightPhaseDetector)
        {
            this.flightDataPreparer = flightDataPreparer;
            this.changePointDetectionService = changePointDetectionService;
            this.signalProcessingUtility = signalProcessingUtility;
            this.featureExtractionUtility = featureExtractionUtility;
            this.segmentLogicUtility = segmentLogicUtility;
            this.flightPhaseDetector = flightPhaseDetector;
        }

        public async Task<FlightPhaseIndexes> GetPhaseIndexesAsync(int flightId, string fieldName)
        {
            IReadOnlyList<double> rawSignalValues =
                await flightDataPreparer.GetParameterValuesAsync(flightId, fieldName);

            int totalSampleCount = rawSignalValues.Count;
            if (totalSampleCount <= 1)
            {
                return new FlightPhaseIndexes(0, 0);
            }

            List<int> rawChangePointIndexes =
                await changePointDetectionService.DetectChangePointsAsync(
                    flightId,
                    fieldName,
                    flightStatus.FullFlight);

            List<int> cleanedChangePointIndexes =
                CleanAndFinalizeChangePoints(rawChangePointIndexes, totalSampleCount);

            List<SegmentBoundary> detectedSegmentBoundaries =
                featureExtractionUtility.BuildSegmentsFromPoints(cleanedChangePointIndexes, totalSampleCount);

            int processedLength;
            double[] processedSignalValues =
                signalProcessingUtility.ApplyZScorePooled(rawSignalValues, out processedLength);

            try
            {
                List<SegmentClassificationResult> segmentResults =
                    BuildSegmentResults(processedSignalValues, detectedSegmentBoundaries);

                List<SegmentBoundary> mergedSegmentBoundaries =
                    ExtractSegmentBoundaries(segmentResults);

                List<SegmentFeatures> segmentFeatures =
                    BuildSegmentFeatures(processedSignalValues, mergedSegmentBoundaries);

                AttachFeatures(segmentResults, segmentFeatures);

                SegmentAnalysisResult phaseBase = new SegmentAnalysisResult
                {
                    Segments = segmentResults,
                    SegmentBoundaries = mergedSegmentBoundaries,
                    AnomalyIndexes = new List<int>(0)
                };

                FlightPhaseIndexes phaseIndexes = flightPhaseDetector.Detect(phaseBase);
                return phaseIndexes;
            }
            finally
            {
                if (processedSignalValues != null && processedSignalValues.Length > 0)
                {
                    ArrayPool<double>.Shared.Return(processedSignalValues, false);
                }
            }
        }

        private List<int> CleanAndFinalizeChangePoints(List<int> rawChangePointIndexes, int totalSampleCount)
        {
            if (rawChangePointIndexes == null || rawChangePointIndexes.Count == 0)
            {
                List<int> onlyFlightEnd = new List<int>(1) { totalSampleCount };
                return onlyFlightEnd;
            }

            rawChangePointIndexes.Sort();

            List<int> cleanedChangePointIndexes = new List<int>(rawChangePointIndexes.Count + 1);

            int lastAcceptedIndex = -1;

            for (int index = 0; index < rawChangePointIndexes.Count; index++)
            {
                int changePointIndex = rawChangePointIndexes[index];

                if (changePointIndex <= 0 || changePointIndex >= totalSampleCount)
                {
                    continue;
                }

                if (changePointIndex == lastAcceptedIndex)
                {
                    continue;
                }

                cleanedChangePointIndexes.Add(changePointIndex);
                lastAcceptedIndex = changePointIndex;
            }

            int lastIndexValue =
                cleanedChangePointIndexes.Count == 0 ? -1 : cleanedChangePointIndexes[cleanedChangePointIndexes.Count - 1];

            if (lastIndexValue != totalSampleCount)
            {
                cleanedChangePointIndexes.Add(totalSampleCount);
            }

            return cleanedChangePointIndexes;
        }

        private List<SegmentClassificationResult> BuildSegmentResults(
            double[] processedSignalValues,
            List<SegmentBoundary> detectedSegmentBoundaries)
        {
            double[] meanValues =
                segmentLogicUtility.ComputeMeansPerSegment(processedSignalValues, detectedSegmentBoundaries);

            List<SegmentClassificationResult> segmentResults =
                segmentLogicUtility.ClassifySegments(processedSignalValues, detectedSegmentBoundaries, meanValues);

            return segmentResults;
        }

        private List<SegmentBoundary> ExtractSegmentBoundaries(List<SegmentClassificationResult> segmentResults)
        {
            int resultCount = segmentResults.Count;

            List<SegmentBoundary> mergedSegmentBoundaries =
                new List<SegmentBoundary>(resultCount);

            for (int index = 0; index < resultCount; index++)
            {
                mergedSegmentBoundaries.Add(segmentResults[index].Segment);
            }

            return mergedSegmentBoundaries;
        }

        private List<SegmentFeatures> BuildSegmentFeatures(
            double[] processedSignalValues,
            List<SegmentBoundary> mergedSegmentBoundaries)
        {
            double[] meanValues =
                segmentLogicUtility.ComputeMeansPerSegment(processedSignalValues, mergedSegmentBoundaries);

            List<SegmentFeatures> segmentFeatures =
                segmentLogicUtility.BuildFeatureList(processedSignalValues, mergedSegmentBoundaries, meanValues);

            return segmentFeatures;
        }

        private void AttachFeatures(
            List<SegmentClassificationResult> segmentResults,
            List<SegmentFeatures> segmentFeatures)
        {
            int segmentCount = segmentResults.Count;
            int featureCount = segmentFeatures.Count;

            int count = segmentCount < featureCount ? segmentCount : featureCount;

            for (int index = 0; index < count; index++)
            {
                segmentResults[index].FeatureValues = segmentFeatures[index];
            }
        }
    }
}
