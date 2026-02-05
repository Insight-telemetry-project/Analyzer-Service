using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Enums;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Algorithms.Pelt.Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Mongo;

namespace Analyzer_Service.Services.Algorithms.Pelt
{
    public class ChangePointDetectionService : IChangePointDetectionService
    {
        private const double SamplePeriodSeconds = 1.0;

        private readonly IPrepareFlightData flightDataPreparer;
        private readonly ISignalPreprocessor signalPreprocessor;
        private readonly IPeltAlgorithm peltAlgorithm;
        private readonly ITuningSettingsFactory tuningSettingsFactory;

        public ChangePointDetectionService(
            IPrepareFlightData flightDataPreparer,
            ISignalPreprocessor signalPreprocessor,
            IPeltAlgorithm peltAlgorithm,
            ITuningSettingsFactory tuningSettingsFactory)
        {
            this.flightDataPreparer = flightDataPreparer;
            this.signalPreprocessor = signalPreprocessor;
            this.peltAlgorithm = peltAlgorithm;
            this.tuningSettingsFactory = tuningSettingsFactory;
        }

        public async Task<List<int>> DetectChangePointsAsync(int masterIndex, string targetFieldName, flightStatus status)
        {
            PeltTuningSettings tuningSettings = tuningSettingsFactory.Get(status);

            IReadOnlyList<double> rawSignal = await flightDataPreparer.GetParameterValuesAsync(masterIndex, targetFieldName);

            

            List<double>? rawSignalList = rawSignal as List<double>;
            

            double[] cleanedSignal = signalPreprocessor.Apply(
                rawSignalList,
                ConstantPelt.HAMPEL_WINDOWSIZE,
                ConstantPelt.HAMPEL_SIGMA_THRESHOLD);

            int minimumSegmentSamples = (int)Math.Round(tuningSettings.MINIMUM_SEGMENT_DURATION_SECONDS / SamplePeriodSeconds);
            if (minimumSegmentSamples < 1)
            {
                minimumSegmentSamples = 1;
            }

            List<int> rawBreakpoints = peltAlgorithm.DetectChangePoints(
                cleanedSignal,
                minimumSegmentSamples,
                tuningSettings.SAMPLING_JUMP,
                tuningSettings.PENALTY_BETA);

            int minimumGapSamples = (int)Math.Round(tuningSettings.MINIMUM_SEGMENT_DURATION_SECONDS / SamplePeriodSeconds);
            if (minimumGapSamples < 1)
            {
                minimumGapSamples = 1;
            }

            List<int> filteredBreakpoints = ApplyMinimumGapBySamples(rawBreakpoints, cleanedSignal.Length, minimumGapSamples);
            return filteredBreakpoints;
        }

        private List<int> ApplyMinimumGapBySamples(List<int> breakpoints, int finalIndex, int minimumGapSamples)
        {
            if (breakpoints == null || breakpoints.Count == 0)
            {
                return new List<int> { finalIndex };
            }

            List<int> acceptedBreakpoints = new List<int>(breakpoints.Count);

            int lastAcceptedIndex = int.MinValue;

            for (int breakpointIndex = 0; breakpointIndex < breakpoints.Count; breakpointIndex++)
            {
                int candidateIndex = breakpoints[breakpointIndex];

                if (candidateIndex < 1)
                {
                    continue;
                }

                if (acceptedBreakpoints.Count == 0 || (candidateIndex - lastAcceptedIndex) >= minimumGapSamples)
                {
                    acceptedBreakpoints.Add(candidateIndex);
                    lastAcceptedIndex = candidateIndex;
                }
            }

            if (acceptedBreakpoints.Count == 0)
            {
                acceptedBreakpoints.Add(finalIndex);
                return acceptedBreakpoints;
            }

            int lastBreakpointIndex = acceptedBreakpoints.Count - 1;
            if (acceptedBreakpoints[lastBreakpointIndex] != finalIndex)
            {
                acceptedBreakpoints[lastBreakpointIndex] = finalIndex;
            }

            return acceptedBreakpoints;
        }
    }
}
