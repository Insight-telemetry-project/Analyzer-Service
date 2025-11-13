using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Algorithms.Pelt.Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Mongo;
using System.Reflection.Metadata;
using Analyzer_Service.Models.Constant;

namespace Analyzer_Service.Services.Algorithms.Pelt
{
    public class ChangePointDetectionService : IChangePointDetectionService
    {
        private readonly IPrepareFlightData prepareData;
        private readonly ISignalPreprocessor signalPreprocessor;
        private readonly IPeltAlgorithm peltAlgorithm;

        public ChangePointDetectionService(
            IPrepareFlightData prepareData,
            ISignalPreprocessor preprocessor,
            IPeltAlgorithm peltAlgorithm)
        {
            this.prepareData = prepareData;
            this.signalPreprocessor = preprocessor;
            this.peltAlgorithm = peltAlgorithm;
        }

        public async Task<IReadOnlyList<int>> DetectChangePointsAsync(int masterIndex, string fieldName)
        {
            const int HampelWindow = 10;
            const double HampelSigma = 2.5;


            const double PenaltyBeta = 2.0;
            const double MinimumSegmentSeconds = 5.0;
            const int Jump = 30;

            (List<double> times, List<double> values) =
                await prepareData.PrepareFlightDataAsync(masterIndex, ConstantFligth.TIMESTEP_COL, fieldName);

            if (values.Count < 10)
                return new List<int>();

            IReadOnlyList<double> cleaned = signalPreprocessor.Apply(
                values, HampelWindow, HampelSigma);

            double samplingFrequency = EstimateSamplingFrequency(times);

            int minSegmentSamples = (int)Math.Round(samplingFrequency * MinimumSegmentSeconds);


            IReadOnlyList<int> rawBreakpoints =
                peltAlgorithm.DetectChangePoints(cleaned, minSegmentSamples, Jump, PenaltyBeta);

            List<int> finalBreakpoints = EnforceMinimumChangePointGap(
                rawBreakpoints.ToList(),
                times.ToArray(),
                MinimumSegmentSeconds
            );

            return finalBreakpoints;

        }
        private static double EstimateSamplingFrequency(IReadOnlyList<double> times)
        {
            if (times.Count < 3)
            {
                return double.NaN;
            }

            int length = times.Count;
            double[] differences = new double[length - 1];

            for (int i = 0; i < length - 1; i++)
            {
                differences[i] = times[i + 1] - times[i];
            }

            List<double> positiveDifferences = new List<double>();
            for (int i = 0; i < differences.Length; i++)
            {
                if (differences[i] > 0.0)
                {
                    positiveDifferences.Add(differences[i]);
                }
            }

            if (positiveDifferences.Count == 0)
            {
                return double.NaN;
            }

            positiveDifferences.Sort();
            int count = positiveDifferences.Count;

            double median;
            if (count % 2 == 1)
            {
                median = positiveDifferences[count / 2];
            }
            else
            {
                median = 0.5 * (positiveDifferences[count / 2 - 1] + positiveDifferences[count / 2]);
            }
            return 1.0 / median;
        }


        private static List<int> EnforceMinimumChangePointGap(
            List<int> breakpoints,
            double[] times,
            double minimumGapSeconds)
        {
            if (minimumGapSeconds <= 0.0 || breakpoints.Count == 0)
            {
                return breakpoints;
            }

            List<int> keptBreakpoints = new List<int>();
            double lastKeptTime = double.NegativeInfinity;

            for (int i = 0; i < breakpoints.Count; i++)
            {
                int endIndex = breakpoints[i];
                double endpointTime = times[endIndex - 1];

                if (keptBreakpoints.Count == 0 || endpointTime - lastKeptTime >= minimumGapSeconds)
                {
                    keptBreakpoints.Add(endIndex);
                    lastKeptTime = endpointTime;
                }
            }

            if (keptBreakpoints[keptBreakpoints.Count - 1] != times.Length)
            {
                keptBreakpoints[keptBreakpoints.Count - 1] = times.Length;
            }

            return keptBreakpoints;
        }

    }

}
