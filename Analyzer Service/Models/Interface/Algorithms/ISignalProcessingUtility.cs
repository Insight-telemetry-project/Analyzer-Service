namespace Analyzer_Service.Models.Interface.Algorithms
{
    public interface ISignalProcessingUtility
    {
        public double[] ApplyHampel(double[] inputValues, int windowSize, double sigma);

        public List<double> ApplyZScore(double[] values);

        public double ComputeMedian(double[] values);

    }
}
