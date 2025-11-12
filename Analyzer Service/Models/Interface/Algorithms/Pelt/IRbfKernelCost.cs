namespace Analyzer_Service.Models.Interface.Algorithms.Pelt
{
    public interface IRbfKernelCost
    {
        int MinimumSize { get; }
        void Fit(IReadOnlyList<double> signal);
        double ComputeError(int segmentStartIndex, int segmentEndIndex);
    }
}
