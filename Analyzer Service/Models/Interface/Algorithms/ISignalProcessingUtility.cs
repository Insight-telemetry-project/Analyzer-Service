namespace Analyzer_Service.Models.Interface.Algorithms
{
    public interface ISignalProcessingUtility
    {
        double[] ApplyHampel(double[] inputValues, int windowSize, double sigma);

        List<double> ApplyZScore(IReadOnlyList<double> values);

        double ComputeMedian(double[] values);

        double ComputeMean(IReadOnlyList<double> values, int startIndex, int endIndex);
    }
}
