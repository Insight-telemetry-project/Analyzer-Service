namespace Analyzer_Service.Models.Interface.Algorithms.Pelt
{
    public interface IRbfKernelCost
    {
        public int MinimumSize { get; }
        public void Fit(List<double> signal);
        public double ComputeError(int segmentStartIndex, int segmentEndIndex);
    }
}
