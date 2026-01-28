namespace Analyzer_Service.Models.Interface.Algorithms
{
    public interface ISignalProcessingUtility
    {
        double[] ApplyHampel(IReadOnlyList<double> inputValues, int windowSize, double sigma);

        double[] ApplyZScore(IReadOnlyList<double> values);

        double ComputeMedian(double[] values);

        double ComputeMedian(double[] buffer, int length);
    }
}
