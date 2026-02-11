namespace Analyzer_Service.Models.Interface.Algorithms
{
    public interface ISignalProcessingUtility
    {
        double[] ApplyHampel(double[] inputValues, int windowSize, double sigma);

        double[] ApplyZScore(double[] values);

        double ComputeMedian(double[] values);

        double ComputeMedian(double[] buffer, int length);
        double[] ApplyZScorePooled(double[] values, out int length);

    }
}
