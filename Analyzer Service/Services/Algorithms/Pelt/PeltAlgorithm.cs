using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Algorithms.Pelt.Analyzer_Service.Models.Interface.Algorithms.Pelt;
namespace Analyzer_Service.Services.Algorithms.Pelt
{
    public class PeltAlgorithm : IPeltAlgorithm
    {
        private readonly IRbfKernelCost costFunction;

        public PeltAlgorithm(IRbfKernelCost costFunction)
        {
            this.costFunction = costFunction;
        }

        public IReadOnlyList<int> DetectChangePoints(
            IReadOnlyList<double> signal,
            int minimumSegmentLength,
            int jump,
            double penaltyBeta)
        {
            costFunction.Fit(signal);
            int sampleCount = signal.Count;
            int effectiveMinimumSegmentLength = Math.Max(minimumSegmentLength, costFunction.MinimumSize);

            Dictionary<int, Dictionary<(int Start, int End), double>> partitionsByEndpoint =
                new Dictionary<int, Dictionary<(int Start, int End), double>>();

            Dictionary<(int Start, int End), double> initialPartition =
                new Dictionary<(int Start, int End), double>();

            initialPartition[(0, 0)] = 0.0;
            partitionsByEndpoint[0] = initialPartition;

            List<int> admissibleEndpoints = new List<int>();
            List<int> candidateBreakpoints = GenerateCandidateBreakpoints(sampleCount, effectiveMinimumSegmentLength, jump);

            foreach (int breakpoint in candidateBreakpoints)
            {
                int newAdmissiblePoint = (int)Math.Floor((breakpoint - effectiveMinimumSegmentLength) / (double)jump);
                newAdmissiblePoint *= jump;
                if (newAdmissiblePoint < 0) newAdmissiblePoint = 0;

                admissibleEndpoints.Add(newAdmissiblePoint);

                Dictionary<(int Start, int End), double> bestPartition = null;
                double bestPartitionCost = double.PositiveInfinity;

                List<Dictionary<(int Start, int End), double>> evaluatedPartitions =
                    EvaluateSubproblems(breakpoint, admissibleEndpoints, partitionsByEndpoint, penaltyBeta);

                foreach (Dictionary<(int Start, int End), double> partition in evaluatedPartitions)
                {
                    double partitionCost = SumPartitionCost(partition);
                    if (partitionCost < bestPartitionCost)
                    {
                        bestPartitionCost = partitionCost;
                        bestPartition = partition;
                    }
                }

                if (bestPartition != null)
                {
                    partitionsByEndpoint[breakpoint] = bestPartition;
                    admissibleEndpoints = PruneAdmissibleEndpoints(admissibleEndpoints, evaluatedPartitions, bestPartitionCost, penaltyBeta);
                }
            }

            int bestEndpoint = partitionsByEndpoint.Keys
                .Where(k => k <= sampleCount)
                .Max();

            Dictionary<(int Start, int End), double> finalPartition = partitionsByEndpoint[bestEndpoint];
            finalPartition.Remove((0, 0));

            List<int> breakpoints = ExtractBreakpoints(finalPartition);
            breakpoints.Sort();
            return breakpoints;
        }

        private List<int> GenerateCandidateBreakpoints(int sampleCount, int minLen, int jump)
        {
            List<int> candidateBreakpoints = new List<int>();
            int index = 0;

            while (index < sampleCount)
            {
                if (index >= minLen)
                {
                    candidateBreakpoints.Add(index);
                }
                index += jump;
            }

            if (candidateBreakpoints.Count == 0 ||
                candidateBreakpoints[candidateBreakpoints.Count - 1] != sampleCount)
            {
                candidateBreakpoints.Add(sampleCount);
            }

            return candidateBreakpoints;
        }

        private List<Dictionary<(int Start, int End), double>> EvaluateSubproblems(
            int breakpoint,
            List<int> admissibleEndpoints,
            Dictionary<int, Dictionary<(int Start, int End), double>> partitionsByEndpoint,
            double penaltyBeta)
        {
            List<Dictionary<(int Start, int End), double>> expandedPartitions =
                new List<Dictionary<(int Start, int End), double>>();

            foreach (int startIndex in admissibleEndpoints)
            {
                if (!partitionsByEndpoint.TryGetValue(startIndex, out Dictionary<(int Start, int End), double> leftPartition))
                {
                    continue;
                }

                Dictionary<(int Start, int End), double> newPartition =
                    new Dictionary<(int Start, int End), double>(leftPartition);

                double segmentCost = costFunction.ComputeError(startIndex, breakpoint) + penaltyBeta;
                newPartition[(startIndex, breakpoint)] = segmentCost;
                expandedPartitions.Add(newPartition);
            }

            return expandedPartitions;
        }

        private List<int> PruneAdmissibleEndpoints(
    List<int> currentEndpoints,
    List<Dictionary<(int Start, int End), double>> evaluatedPartitions,
    double bestCost,
    double penaltyBeta)
        {
            List<int> pruned = new List<int>();

            for (int i = 0; i < currentEndpoints.Count; i++)
            {
                double cost = SumPartitionCost(evaluatedPartitions[i]);

                if (cost <= bestCost + penaltyBeta)
                {
                    pruned.Add(currentEndpoints[i]);
                }
            }

            return pruned;
        }

        private double SumPartitionCost(Dictionary<(int Start, int End), double> partition)
        {
            double sum = 0.0;
            foreach (double value in partition.Values) sum += value;
            return sum;
        }

        private List<int> ExtractBreakpoints(Dictionary<(int Start, int End), double> finalPartition)
        {
            List<int> breakpoints = new List<int>();
            foreach ((int _, int End) segment in finalPartition.Keys)
            {
                breakpoints.Add(segment.End);
            }
            return breakpoints;
        }
    }
}
