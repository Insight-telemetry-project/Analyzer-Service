using System;
using System.Linq;
using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms.AnomalyDetector;

namespace Analyzer_Service.Services.Algorithms.AnomalyDetector
{
    public class PatternHashingUtility : IPatternHashingUtility
    {
        private readonly ISignalProcessingUtility signalProcessingUtility;

        public PatternHashingUtility(ISignalProcessingUtility signalProcessingUtility)
        {
            this.signalProcessingUtility = signalProcessingUtility;
        }

        public string ComputeHash(double[] processedSignal, SegmentBoundary segmentBoundary)
        {
            int segmentLength = segmentBoundary.EndIndex - segmentBoundary.StartIndex;

            if (segmentLength <= ConstantPelt.MINIMUM_SEGMENT_LENGTH)
            {
                double[] hashVector = this.BuildHashVector(processedSignal, segmentBoundary);
                return string.Join(ConstantAnomalyDetection.HASH_SPLIT, hashVector);
            }

            double[] normalizedTimeArray = BuildNormalizedTimeArray(segmentLength);
            double[] segmentValueArray = BuildSegmentValueArray(processedSignal, segmentBoundary);

            double[] resamplingGrid = CreateResamplingGrid(ConstantAnomalyDetection.SHAPE_LENGTH);

            double[] resampledValues =
                ResampleSegmentToFixedLength(normalizedTimeArray, segmentValueArray, resamplingGrid);

            return BuildHashFromResampledValues(resampledValues);
        }

        private static string BuildShortSegmentHash(double[] processedSignal, SegmentBoundary segmentBoundary)
        {
            double repeatedValue = processedSignal[segmentBoundary.StartIndex];

            double[] repeatedValueArray =
                Enumerable.Repeat(repeatedValue, ConstantAnomalyDetection.SHAPE_LENGTH).ToArray();

            return string.Join(ConstantAnomalyDetection.HASH_SPLIT, repeatedValueArray);
        }

        private static double[] BuildNormalizedTimeArray(int segmentLength)
        {
            double safeLength = Math.Max(2, segmentLength);
            double[] normalizedTimeArray = new double[segmentLength];

            for (int indexInSegment = 0; indexInSegment < segmentLength; indexInSegment++)
            {
                normalizedTimeArray[indexInSegment] = (double)indexInSegment / (safeLength - 1);
            }

            return normalizedTimeArray;
        }

        private static double[] BuildSegmentValueArray(double[] processedSignal, SegmentBoundary segmentBoundary)
        {
            int segmentLength = segmentBoundary.EndIndex - segmentBoundary.StartIndex;
            double[] segmentValueArray = new double[segmentLength];

            for (int segmentOffset = 0; segmentOffset < segmentLength; segmentOffset++)
            {
                int sourceIndex = segmentBoundary.StartIndex + segmentOffset;
                segmentValueArray[segmentOffset] = processedSignal[sourceIndex];
            }

            return segmentValueArray;
        }

        private static double[] CreateResamplingGrid(int targetLength)
        {
            double[] resamplingGrid = new double[targetLength];

            for (int gridIndex = 0; gridIndex < targetLength; gridIndex++)
            {
                resamplingGrid[gridIndex] = (double)gridIndex / (targetLength - 1);
            }

            return resamplingGrid;
        }

        private string BuildHashFromResampledValues(double[] resampledValues)
        {
            double[] zScoreValues = signalProcessingUtility.ApplyZScore(resampledValues);

            double[] roundedZScores =
                zScoreValues
                    .Select(value => Math.Round(value, ConstantAnomalyDetection.ROUND_DECIMALS))
                    .ToArray();

            return string.Join(ConstantAnomalyDetection.HASH_SPLIT, roundedZScores);
        }

        private static double[] ResampleSegmentToFixedLength(
            double[] normalizedTimeArray,
            double[] segmentValueArray,
            double[] resamplingGrid)
        {
            double[] resampledOutputArray = new double[resamplingGrid.Length];

            for (int gridIndex = 0; gridIndex < resamplingGrid.Length; gridIndex++)
            {
                double targetNormalizedTime = resamplingGrid[gridIndex];

                double interpolatedValue =
                    InterpolateAtTargetTime(
                        normalizedTimeArray,
                        segmentValueArray,
                        targetNormalizedTime);

                resampledOutputArray[gridIndex] = interpolatedValue;
            }

            return resampledOutputArray;
        }

        private static double InterpolateAtTargetTime(
            double[] normalizedTimeArray,
            double[] segmentValueArray,
            double targetNormalizedTime)
        {
            int lastIndex = normalizedTimeArray.Length - 1;

            if (targetNormalizedTime <= normalizedTimeArray[0])
                return segmentValueArray[0];

            if (targetNormalizedTime >= normalizedTimeArray[lastIndex])
                return segmentValueArray[lastIndex];

            int lowerIndex = FindLowerTimeIndex(normalizedTimeArray, targetNormalizedTime);
            int upperIndex = lowerIndex + 1;

            double lowerTime = normalizedTimeArray[lowerIndex];
            double upperTime = normalizedTimeArray[upperIndex];

            double lowerValue = segmentValueArray[lowerIndex];
            double upperValue = segmentValueArray[upperIndex];
            
            double interpolationWeight =
                (targetNormalizedTime - lowerTime) / (upperTime - lowerTime);

            return lowerValue + interpolationWeight * (upperValue - lowerValue);
        }

        private static int FindLowerTimeIndex(double[] normalizedTimeArray, double targetNormalizedTime)
        {
            return Array.FindLastIndex(
                normalizedTimeArray,
                timeValue => timeValue <= targetNormalizedTime);
        }
        public double[] ComputeHashVector(double[] signalValues, SegmentBoundary segmentBoundary)
        {
            return this.BuildHashVector(signalValues, segmentBoundary);
        }
        private double[] BuildHashVector(double[] processedSignal, SegmentBoundary segmentBoundary)
        {
            int segmentLength = segmentBoundary.EndIndex - segmentBoundary.StartIndex;

            if (segmentLength <= ConstantPelt.MINIMUM_SEGMENT_LENGTH)
            {
                double repeatedValue = processedSignal[segmentBoundary.StartIndex];

                double[] repeatedValueArray =
                    Enumerable.Repeat(repeatedValue, ConstantAnomalyDetection.SHAPE_LENGTH).ToArray();

                double[] zScoreValuesForShort = signalProcessingUtility.ApplyZScore(repeatedValueArray);

                double[] roundedShort =
                    zScoreValuesForShort
                        .Select(value => Math.Round(value, ConstantAnomalyDetection.ROUND_DECIMALS))
                        .ToArray();

                return roundedShort;
            }

            double[] normalizedTimeArray = BuildNormalizedTimeArray(segmentLength);
            double[] segmentValueArray = BuildSegmentValueArray(processedSignal, segmentBoundary);
            double[] resamplingGrid = CreateResamplingGrid(ConstantAnomalyDetection.SHAPE_LENGTH);

            double[] resampledValues =
                ResampleSegmentToFixedLength(normalizedTimeArray, segmentValueArray, resamplingGrid);

            double[] zScoreValues = signalProcessingUtility.ApplyZScore(resampledValues);

            double[] roundedZScores =
                zScoreValues
                    .Select(value => Math.Round(value, ConstantAnomalyDetection.ROUND_DECIMALS))
                    .ToArray();

            return roundedZScores;
        }



    }
}
