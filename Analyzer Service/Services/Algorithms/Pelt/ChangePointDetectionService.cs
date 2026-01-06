using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Algorithms.Pelt.Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Mongo;

namespace Analyzer_Service.Services.Algorithms.Pelt
{
    public class ChangePointDetectionService : IChangePointDetectionService
    {
        private readonly IPrepareFlightData flightDataPreparer; // Discussion: use _ prefix for private fields
        private readonly ISignalPreprocessor signalPreprocessor;
        private readonly IPeltAlgorithm peltAlgorithm;
        private readonly ISignalProcessingUtility signalProcessingUtility;

        public ChangePointDetectionService(
            IPrepareFlightData flightDataPreparer,
            ISignalPreprocessor signalPreprocessor,
            IPeltAlgorithm peltAlgorithm,
            ISignalProcessingUtility signalProcessingUtility)
        {
            this.flightDataPreparer = flightDataPreparer;
            this.signalPreprocessor = signalPreprocessor;
            this.peltAlgorithm = peltAlgorithm;
            this.signalProcessingUtility = signalProcessingUtility;
        }

        public async Task<List<int>> DetectChangePointsAsync(int masterIndex, string targetFieldName)
        {
            SignalSeries series =await flightDataPreparer.PrepareFlightDataAsync( // Discussion: each time fetching from the mongo, instead of using the same cursor
                masterIndex,
                ConstantFligth.TIMESTEP_COL,
                targetFieldName);

            List<double> timeSeries = series.Time; // Discussion: you are using simple types like double all along the project without labeling them, encapsulating these in objects would make it more readable
            List<double> signalSeries = series.Values;


            List<double> cleanedSignal =
                signalPreprocessor
                    .Apply(signalSeries, ConstantPelt.HAMPEL_WINDOWSIZE, ConstantPelt.HAMPEL_SIGMA_THRESHOLD)
                    .ToList();

            double samplingFrequency = ComputeSamplingFrequency(timeSeries);

            int minimumSegmentSamples =
                (int)Math.Round(samplingFrequency * ConstantPelt.MINIMUM_SEGMENT_DURATION_SECONDS);

            List<int> rawBreakpoints =
                peltAlgorithm
                    .DetectChangePoints(
                        cleanedSignal,
                        minimumSegmentSamples,
                        ConstantPelt.SAMPLING_JUMP,
                        ConstantPelt.PENALTY_BETA)
                    .ToList(); // Discussion: unnecessary memory allocation here with ToList




            List<int> filtered =
                ApplyMinimumGap(rawBreakpoints, timeSeries.ToArray(), ConstantPelt.MINIMUM_SEGMENT_DURATION_SECONDS);

            return filtered;
        }

        private double ComputeSamplingFrequency(IReadOnlyList<double> timeSeries) // Discussion: you are passing a list here, so why are you suddenly using IReadOnlyList?
        {
            int count = timeSeries.Count - 1;

            double[] differences = new double[count]; // Discussion: allocating memory unnecessarily here
            // Discussion: you can use Linq to compute the differences without allocating an array
            for (int index = 0; index < count; index++)
            {
                differences[index] = timeSeries[index + 1] - timeSeries[index];
            }

            double median = signalProcessingUtility.ComputeMedian(differences);
            return 1.0 / median;
        }

        private List<int> ApplyMinimumGap(List<int> breakpoints, double[] timeSeries, double minGap)
        {
            List<int> result = new List<int>();
            double lastTime = double.NegativeInfinity;

            for (int indexBreakPoint = 0; indexBreakPoint < breakpoints.Count; indexBreakPoint++)
            {
                int index = breakpoints[indexBreakPoint];
                double timeValue = timeSeries[index - 1];

                if (result.Count == 0 || timeValue - lastTime >= minGap)
                {
                    result.Add(index);
                    lastTime = timeValue;
                }
            }

            int finalIndex = timeSeries.Length;
            int last = result.Count - 1;

            if (result[last] != finalIndex)
            {
                result[last] = finalIndex;
            }

            return result;
        }
    }
}
