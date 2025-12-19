using Analyzer_Service.Models.Dto;
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



        public List<int> DetectChangePoints( List<double> signalValues, int minimumSegmentLength, int jumpSize, double penaltyBeta)
        {
            costFunction.Fit(signalValues);

            int sampleCount = signalValues.Count;
            int effectiveMinimumSegmentLength =
                Math.Max(minimumSegmentLength, costFunction.MinimumSize);

            Dictionary<int, Dictionary<SegmentBoundary, double>> partitionsByEndpoint =
                new Dictionary<int, Dictionary<SegmentBoundary, double>>();

            Dictionary<SegmentBoundary, double> initialPartition =
                new Dictionary<SegmentBoundary, double>
                {
                    [new SegmentBoundary(0, 0)] = 0.0
                };

            partitionsByEndpoint[0] = initialPartition;

            List<int> admissibleEndpoints = new List<int>();
            List<int> candidateBreakpoints =
                GenerateCandidateBreakpoints(sampleCount, effectiveMinimumSegmentLength, jumpSize);

            foreach (int breakpoint in candidateBreakpoints)
            {
                admissibleEndpoints = ProcessBreakpoint(
                    breakpoint,
                    admissibleEndpoints,
                    partitionsByEndpoint,
                    effectiveMinimumSegmentLength,
                    jumpSize,
                    penaltyBeta);
            }

            int bestEndpoint =
                partitionsByEndpoint.Keys.Where(key => key <= sampleCount).Max();

            Dictionary<SegmentBoundary, double> finalPartition =
                partitionsByEndpoint[bestEndpoint];

            finalPartition.Remove(new SegmentBoundary(0, 0));

            List<int> breakpoints = ExtractBreakpoints(finalPartition);
            breakpoints.Sort();
            return breakpoints;
        }


        private List<int> ProcessBreakpoint(
            int breakpoint,
            List<int> admissibleEndpoints,
            Dictionary<int, Dictionary<SegmentBoundary, double>> partitionsByEndpoint,
            int effectiveMinimumSegmentLength,
            int jumpSize,
            double penaltyBeta)
        {
            int newAdmissiblePoint =
                (int)Math.Floor((breakpoint - effectiveMinimumSegmentLength) / (double)jumpSize);

            newAdmissiblePoint *= jumpSize;
            if (newAdmissiblePoint < 0)
            {
                newAdmissiblePoint = 0;
            }

            admissibleEndpoints.Add(newAdmissiblePoint);

            List<Dictionary<SegmentBoundary, double>> evaluatedPartitions =
                EvaluateSubproblems(breakpoint, admissibleEndpoints, partitionsByEndpoint, penaltyBeta);

            Dictionary<SegmentBoundary, double> bestPartition = null;
            double bestCost = double.PositiveInfinity;

            foreach (Dictionary<SegmentBoundary, double> partition in evaluatedPartitions)
            {
                double cost = SumPartitionCost(partition);

                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestPartition = partition;
                }
            }

            if (bestPartition != null)
            {
                partitionsByEndpoint[breakpoint] = bestPartition;

                admissibleEndpoints =
                    PruneAdmissibleEndpoints(admissibleEndpoints, evaluatedPartitions, bestCost, penaltyBeta);
            }

            return admissibleEndpoints;
        }


        private List<int> GenerateCandidateBreakpoints(int sampleCount, int minimumLength, int jumpSize)
        {
            List<int> breakpoints = new List<int>();
            int index = 0;

            while (index < sampleCount)
            {
                if (index >= minimumLength)
                {
                    breakpoints.Add(index);
                }
                index += jumpSize;
            }

            if (breakpoints.Count == 0 || breakpoints[^1] != sampleCount)
            {
                breakpoints.Add(sampleCount);
            }

            return breakpoints;
        }

        private List<Dictionary<SegmentBoundary, double>> EvaluateSubproblems(
            int breakpoint,
            List<int> admissibleEndpoints,
            Dictionary<int, Dictionary<SegmentBoundary, double>> partitionsByEndpoint,
            double penaltyBeta)
        {
            List<Dictionary<SegmentBoundary, double>> expanded = new List<Dictionary<SegmentBoundary, double>>();

            foreach (int startIndex in admissibleEndpoints)
            {
                if (!partitionsByEndpoint.TryGetValue(startIndex, out Dictionary<SegmentBoundary, double> leftPartition))
                {
                    continue;
                }

                Dictionary<SegmentBoundary, double> newPartition =
                    new Dictionary<SegmentBoundary, double>(leftPartition);

                double segmentCost = costFunction.ComputeError(startIndex, breakpoint) + penaltyBeta;
                newPartition[new SegmentBoundary(startIndex, breakpoint)] = segmentCost;

                expanded.Add(newPartition);
            }

            return expanded;
        }

        private List<int> PruneAdmissibleEndpoints(
            List<int> currentEndpoints,
            List<Dictionary<SegmentBoundary, double>> evaluatedPartitions,
            double bestCost,
            double penaltyBeta)
        {
            List<int> pruned = new List<int>();

            for (int indexEndpoints = 0; indexEndpoints < currentEndpoints.Count; indexEndpoints++)
            {
                double partitionCost = SumPartitionCost(evaluatedPartitions[indexEndpoints]);

                if (partitionCost <= bestCost + penaltyBeta)
                {
                    pruned.Add(currentEndpoints[indexEndpoints]);
                }
            }

            return pruned;
        }

        private double SumPartitionCost(Dictionary<SegmentBoundary, double> partition)
        {
            double total = 0.0;

            foreach (double cost in partition.Values)
            {
                total += cost;
            }

            return total;
        }

        private List<int> ExtractBreakpoints(Dictionary<SegmentBoundary, double> finalPartition)
        {
            List<int> breakpoints = new List<int>();

            foreach (SegmentBoundary boundary in finalPartition.Keys)
            {
                if (boundary.EndIndex != 0)  
                {
                    breakpoints.Add(boundary.EndIndex);
                }
            }

            return breakpoints;
        }

    }
}
