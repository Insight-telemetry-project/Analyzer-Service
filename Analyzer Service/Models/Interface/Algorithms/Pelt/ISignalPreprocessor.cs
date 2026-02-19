namespace Analyzer_Service.Models.Interface.Algorithms.Pelt
{
    public interface ISignalPreprocessor
    {
        double[] Apply(double[] inputSignalValues,int hampelWindowSize,double hampelSigmaThreshold);
    }
}
