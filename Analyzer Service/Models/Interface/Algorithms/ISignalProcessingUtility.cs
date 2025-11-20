namespace Analyzer_Service.Models.Interface.Algorithms
{
    public interface ISignalProcessingUtility
    {
        double[] ApplyHampel(double[] inputValues, int windowSize, double sigma);

        List<double> ApplyZScore(double[] values);

        double ComputeMedian(double[] values);

        double ComputeMean(List<double> values, int startIndex, int endIndex);
    }
}
