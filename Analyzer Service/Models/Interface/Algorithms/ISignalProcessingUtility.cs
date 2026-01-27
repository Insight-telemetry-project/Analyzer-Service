namespace Analyzer_Service.Models.Interface.Algorithms
{
    public interface ISignalProcessingUtility
    {
        double[] ApplyHampel(double[] inputValues, int windowSize, double sigma);

        double[] ApplyZScore(double[] values);

        double ComputeMedian(double[] values);
    }
}
