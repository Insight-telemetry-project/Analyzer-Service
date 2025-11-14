using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Algorithms.Pelt.Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Mongo;

namespace Analyzer_Service.Services.Algorithms.Pelt
{
    public class ChangePointDetectionService : IChangePointDetectionService
    {
        private readonly IPrepareFlightData flightDataPreparer;
        private readonly ISignalPreprocessor signalPreprocessor;
        private readonly IPeltAlgorithm peltAlgorithm;

        public ChangePointDetectionService(
            IPrepareFlightData flightDataPreparer,
            ISignalPreprocessor signalPreprocessor,
            IPeltAlgorithm peltAlgorithm)
        {
            this.flightDataPreparer = flightDataPreparer;
            this.signalPreprocessor = signalPreprocessor;
            this.peltAlgorithm = peltAlgorithm;
        }

        public async Task<List<int>> DetectChangePointsAsync(int masterIndex, string targetFieldName)
        {
            

            (List<double> timeSeries, List<double> signalSeries) =
                await flightDataPreparer.PrepareFlightDataAsync(
                    masterIndex,
                    ConstantFligth.TIMESTEP_COL,
                    targetFieldName
                );

            List<double> cleanedSignal =
                signalPreprocessor.Apply(signalSeries, ConstantPelt.HAMPEL_WINDOWSIZE, ConstantPelt.HAMPEL_SIGMA_THRESHOLD)
                .ToList();

            double samplingFrequency = EstimateSamplingFrequency(timeSeries);

            int minimumSegmentSampleCount =
                (int)Math.Round(samplingFrequency * ConstantPelt.MINIMUM_SEGMENT_DURATION_SECONDS);

            List<int> rawDetectedBreakpoints =
                peltAlgorithm
                .DetectChangePoints(cleanedSignal, minimumSegmentSampleCount, ConstantPelt.SAMPLING_JUMP, ConstantPelt.PENALTY_BETA)
                .ToList();

            List<int> filteredBreakpoints =
                EnforceMinimumChangePointGap(
                    rawDetectedBreakpoints,
                    timeSeries.ToArray(),
                    ConstantPelt.MINIMUM_SEGMENT_DURATION_SECONDS
                );

            return filteredBreakpoints;
        }

        private static double EstimateSamplingFrequency(IReadOnlyList<double> timeSeries)
        {
            int sampleCount = timeSeries.Count;
            double[] sampleDifferences = new double[sampleCount - 1];

            for (int index = 0; index < sampleCount - 1; index++)
            {
                sampleDifferences[index] = timeSeries[index + 1] - timeSeries[index];
            }

            List<double> positiveDifferences = new List<double>();

            foreach (double difference in sampleDifferences)
            {
                if (difference > 0.0)
                {
                    positiveDifferences.Add(difference);
                }
            }

            positiveDifferences.Sort();
            int differenceCount = positiveDifferences.Count;

            double medianDifference;
            if (differenceCount % 2 == 1)
            {
                medianDifference = positiveDifferences[differenceCount / 2];
            }
            else
            {
                medianDifference =
                    0.5 * (positiveDifferences[differenceCount / 2 - 1] +
                           positiveDifferences[differenceCount / 2]);
            }

            return 1.0 / medianDifference;
        }


        private static List<int> EnforceMinimumChangePointGap(
            List<int> detectedBreakpoints,
            double[] timeSeries,
            double minimumGapSeconds)
        {
            List<int> filteredBreakpoints = new List<int>();
            double lastAcceptedTime = double.NegativeInfinity;

            foreach (int breakpointIndex in detectedBreakpoints)
            {
                double breakpointTime = timeSeries[breakpointIndex - 1];

                if (filteredBreakpoints.Count == 0 ||
                    breakpointTime - lastAcceptedTime >= minimumGapSeconds)
                {
                    filteredBreakpoints.Add(breakpointIndex);
                    lastAcceptedTime = breakpointTime;
                }
            }

            int lastTimeIndex = timeSeries.Length;

            int lastBreakpointIndex = filteredBreakpoints.Count - 1;

            if (filteredBreakpoints[lastBreakpointIndex] != lastTimeIndex)
            {
                filteredBreakpoints[lastBreakpointIndex] = lastTimeIndex;
            }

            return filteredBreakpoints;
        }
    }
}
