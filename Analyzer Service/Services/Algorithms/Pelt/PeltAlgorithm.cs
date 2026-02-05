using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Algorithms.Pelt.Analyzer_Service.Models.Interface.Algorithms.Pelt;
using System.Buffers;

namespace Analyzer_Service.Services.Algorithms.Pelt
{
    public class PeltAlgorithm : IPeltAlgorithm
    {
        private readonly IRbfKernelCost costFunction;

        public PeltAlgorithm(IRbfKernelCost costFunction)
        {
            this.costFunction = costFunction;
        }

        public List<int> DetectChangePoints(double[] signalValues, int minimumSegmentLength, int jumpSize, double penaltyBeta)
        {
            costFunction.Fit(signalValues);

            int sampleCount = signalValues.Length;
            if (sampleCount <= 0)
            {
                return new List<int> { 0 };
            }

            int effectiveMinimumSegmentLength = Math.Max(minimumSegmentLength, costFunction.MinimumSize);
            if (effectiveMinimumSegmentLength < 1)
            {
                effectiveMinimumSegmentLength = 1;
            }

            if (jumpSize < 1)
            {
                jumpSize = 1;
            }

            List<int> candidateEndpoints = GenerateCandidateEndpoints(sampleCount, effectiveMinimumSegmentLength, jumpSize);

            double[] bestCostByIndex = new double[sampleCount + 1];
            int[] bestPreviousByIndex = new int[sampleCount + 1];
            bool[] isAdmissibleIndex = new bool[sampleCount + 1];

            for (int index = 0; index <= sampleCount; index++)
            {
                bestCostByIndex[index] = double.PositiveInfinity;
                bestPreviousByIndex[index] = -1;
            }

            bestCostByIndex[0] = 0.0;
            bestPreviousByIndex[0] = 0;

            List<int> admissibleStartIndices = new List<int>(Math.Max(8, candidateEndpoints.Count));
            admissibleStartIndices.Add(0);
            isAdmissibleIndex[0] = true;

            for (int candidateIndex = 0; candidateIndex < candidateEndpoints.Count; candidateIndex++)
            {
                int segmentEndIndex = candidateEndpoints[candidateIndex];

                if (segmentEndIndex < effectiveMinimumSegmentLength)
                {
                    continue;
                }

                int newAdmissibleStartIndex = ((segmentEndIndex - effectiveMinimumSegmentLength) / jumpSize) * jumpSize;
                if (newAdmissibleStartIndex < 0)
                {
                    newAdmissibleStartIndex = 0;
                }

                if (!isAdmissibleIndex[newAdmissibleStartIndex])
                {
                    admissibleStartIndices.Add(newAdmissibleStartIndex);
                    isAdmissibleIndex[newAdmissibleStartIndex] = true;
                }

                int admissibleCount = admissibleStartIndices.Count;
                double[] segmentCostsBuffer = ArrayPool<double>.Shared.Rent(admissibleCount);

                double bestTotalCost = double.PositiveInfinity;
                int bestStartIndex = -1;

                try
                {
                    for (int startCandidatePosition = 0; startCandidatePosition < admissibleCount; startCandidatePosition++)
                    {
                        int segmentStartIndex = admissibleStartIndices[startCandidatePosition];

                        double segmentCost = double.PositiveInfinity;

                        if (segmentStartIndex != segmentEndIndex &&
                            (segmentEndIndex - segmentStartIndex) >= effectiveMinimumSegmentLength &&
                            !double.IsPositiveInfinity(bestCostByIndex[segmentStartIndex]))
                        {
                            segmentCost = costFunction.ComputeError(segmentStartIndex, segmentEndIndex);
                        }

                        segmentCostsBuffer[startCandidatePosition] = segmentCost;

                        if (double.IsPositiveInfinity(segmentCost))
                        {
                            continue;
                        }

                        double totalCost = bestCostByIndex[segmentStartIndex] + segmentCost + penaltyBeta;

                        if (totalCost < bestTotalCost)
                        {
                            bestTotalCost = totalCost;
                            bestStartIndex = segmentStartIndex;
                        }
                    }

                    if (bestStartIndex == -1)
                    {
                        continue;
                    }

                    bestCostByIndex[segmentEndIndex] = bestTotalCost;
                    bestPreviousByIndex[segmentEndIndex] = bestStartIndex;

                    List<int> prunedAdmissibleStartIndices = new List<int>(admissibleCount);

                    for (int startCandidatePosition = 0; startCandidatePosition < admissibleCount; startCandidatePosition++)
                    {
                        int segmentStartIndex = admissibleStartIndices[startCandidatePosition];

                        if (segmentStartIndex == segmentEndIndex)
                        {
                            continue;
                        }

                        double segmentCost = segmentCostsBuffer[startCandidatePosition];

                        if (double.IsPositiveInfinity(segmentCost) || double.IsPositiveInfinity(bestCostByIndex[segmentStartIndex]))
                        {
                            isAdmissibleIndex[segmentStartIndex] = false;
                            continue;
                        }

                        double totalCost = bestCostByIndex[segmentStartIndex] + segmentCost + penaltyBeta;

                        if (totalCost <= bestTotalCost + penaltyBeta)
                        {
                            prunedAdmissibleStartIndices.Add(segmentStartIndex);
                        }
                        else
                        {
                            isAdmissibleIndex[segmentStartIndex] = false;
                        }
                    }

                    if (!isAdmissibleIndex[newAdmissibleStartIndex])
                    {
                        prunedAdmissibleStartIndices.Add(newAdmissibleStartIndex);
                        isAdmissibleIndex[newAdmissibleStartIndex] = true;
                    }
                    else
                    {
                        bool exists = false;
                        for (int index = 0; index < prunedAdmissibleStartIndices.Count; index++)
                        {
                            if (prunedAdmissibleStartIndices[index] == newAdmissibleStartIndex)
                            {
                                exists = true;
                                break;
                            }
                        }

                        if (!exists)
                        {
                            prunedAdmissibleStartIndices.Add(newAdmissibleStartIndex);
                        }
                    }

                    admissibleStartIndices = prunedAdmissibleStartIndices;
                }
                finally
                {
                    ArrayPool<double>.Shared.Return(segmentCostsBuffer, true);
                }
            }

            int chosenEndIndex = sampleCount;
            if (bestPreviousByIndex[chosenEndIndex] == -1)
            {
                int lastReachableCandidate = -1;

                for (int index = candidateEndpoints.Count - 1; index >= 0; index--)
                {
                    int endpoint = candidateEndpoints[index];
                    if (!double.IsPositiveInfinity(bestCostByIndex[endpoint]))
                    {
                        lastReachableCandidate = endpoint;
                        break;
                    }
                }

                if (lastReachableCandidate == -1)
                {
                    return new List<int> { sampleCount };
                }

                chosenEndIndex = lastReachableCandidate;
            }

            List<int> breakpoints = BacktrackBreakpoints(bestPreviousByIndex, chosenEndIndex);

            if (breakpoints.Count == 0)
            {
                breakpoints.Add(sampleCount);
            }
            else
            {
                int lastBreakpointPosition = breakpoints.Count - 1;
                if (breakpoints[lastBreakpointPosition] != sampleCount)
                {
                    breakpoints[lastBreakpointPosition] = sampleCount;
                }
            }

            breakpoints.Sort();
            return breakpoints;
        }

        private List<int> GenerateCandidateEndpoints(int sampleCount, int minimumLength, int jumpSize)
        {
            List<int> candidateEndpoints = new List<int>();

            int currentIndex = 0;
            while (currentIndex < sampleCount)
            {
                if (currentIndex >= minimumLength)
                {
                    candidateEndpoints.Add(currentIndex);
                }

                currentIndex += jumpSize;
            }

            if (candidateEndpoints.Count == 0 || candidateEndpoints[candidateEndpoints.Count - 1] != sampleCount)
            {
                candidateEndpoints.Add(sampleCount);
            }

            return candidateEndpoints;
        }

        private List<int> BacktrackBreakpoints(int[] bestPreviousByIndex, int endIndex)
        {
            List<int> breakpoints = new List<int>();

            int currentIndex = endIndex;
            while (currentIndex > 0)
            {
                breakpoints.Add(currentIndex);

                int previousIndex = bestPreviousByIndex[currentIndex];
                if (previousIndex <= 0)
                {
                    break;
                }

                currentIndex = previousIndex;
            }

            return breakpoints;
        }
    }
}
