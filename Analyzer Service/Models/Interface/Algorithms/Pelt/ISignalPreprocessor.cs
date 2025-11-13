namespace Analyzer_Service.Models.Interface.Algorithms.Pelt
{
    public interface ISignalPreprocessor
    {
        IReadOnlyList<double> Apply(
            IReadOnlyList<double> values,
            int hampelWindow,
            double hampelSigma);
    }
}
