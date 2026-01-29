namespace Analyzer_Service.Models.Interface.Algorithms.Pelt
{
    public interface ISignalPreprocessor
    {
        public double[] Apply(List<double> values, int hampelWindow, double hampelSigma);
    }
}
