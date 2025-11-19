using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms.AnomalyDetector;
using Analyzer_Service.Models.Constant;
namespace Analyzer_Service.Services.Algorithms.AnomalyDetector
{
    public class PatternHashingUtility : IPatternHashingUtility
    {
        private readonly ISignalProcessingUtility signalProcessingUtility;

        public PatternHashingUtility(ISignalProcessingUtility signalProcessingUtility)
        {
            this.signalProcessingUtility = signalProcessingUtility;
        }

        public string ComputeHash(
            List<double> timeSeries,
            List<double> processedSignal,
            SegmentBoundary segmentBoundary)
        {
            int segmentLength = segmentBoundary.EndIndex - segmentBoundary.StartIndex;

            if (segmentLength <= 2)
            {
                double value = processedSignal[segmentBoundary.StartIndex];
                double[] repeated = Enumerable.Repeat(value, ConstantAnomalyDetection.SHAPE_LENGTH).ToArray();
                return string.Join(",", repeated);
            }

            double startTime = timeSeries[segmentBoundary.StartIndex];
            double endTime = timeSeries[segmentBoundary.EndIndex - 1];
            double duration = endTime - startTime;

            if (duration <= 0.0)
            {
                duration = 1.0;
            }

            double[] normalizedTime = new double[segmentLength];
            double[] segmentValues = new double[segmentLength];

            for (int index = 0; index < segmentLength; index++)
            {
                normalizedTime[index] =
                    (timeSeries[segmentBoundary.StartIndex + index] - startTime) / duration;

                segmentValues[index] = processedSignal[segmentBoundary.StartIndex + index];
            }

            double[] grid = new double[ConstantAnomalyDetection.SHAPE_LENGTH];
            for (int index = 0; index < ConstantAnomalyDetection.SHAPE_LENGTH; index++)
            {
                grid[index] = (double)index / (ConstantAnomalyDetection.SHAPE_LENGTH - 1);
            }

            double[] interpolatedValues = Interpolate(normalizedTime, segmentValues, grid);

            List<double> zScoreValues =
                signalProcessingUtility.ApplyZScore(interpolatedValues);

            double[] roundedValues =
                zScoreValues
                    .Select(value => Math.Round(value, ConstantAnomalyDetection.ROUND_DECIMALS))
                    .ToArray();

            string hash = string.Join(",", roundedValues);
            return hash;
        }

        private static double[] Interpolate(double[] normalizedTime, double[] values, double[] grid)
        {
            int timeCount = normalizedTime.Length;
            double[] interpolated = new double[grid.Length];

            for (int gridIndex = 0; gridIndex < grid.Length; gridIndex++)
            {
                double target = grid[gridIndex];

                if (target <= normalizedTime[0])
                {
                    interpolated[gridIndex] = values[0];
                    continue;
                }

                if (target >= normalizedTime[timeCount - 1])
                {
                    interpolated[gridIndex] = values[timeCount - 1];
                    continue;
                }

                int lowerIndex = Array.FindLastIndex(normalizedTime, timeValue => timeValue <= target);
                int upperIndex = lowerIndex + 1;

                double lowerTime = normalizedTime[lowerIndex];
                double upperTime = normalizedTime[upperIndex];

                double lowerValue = values[lowerIndex];
                double upperValue = values[upperIndex];

                double weight = (target - lowerTime) / (upperTime - lowerTime);
                interpolated[gridIndex] = lowerValue + weight * (upperValue - lowerValue);
            }

            return interpolated;
        }
    }
}
