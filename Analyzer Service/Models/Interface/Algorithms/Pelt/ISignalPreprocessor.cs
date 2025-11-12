namespace Analyzer_Service.Models.Interface.Algorithms.Pelt
{
    public interface ISignalPreprocessor
    {
        IReadOnlyList<double> Apply(
            IReadOnlyList<double> values,
            bool useHampel,
            int hampelWindow,
            double hampelSigma,
            bool useZScore);
    }
}
