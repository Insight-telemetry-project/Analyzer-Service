namespace Analyzer_Service.Models.Interface.Algorithms.Ccm
{
    public interface ICcmCausalityAnalyzer
    {
        double ComputeCausality(List<double> xSeries, List<double> ySeries, int embeddingDim, int delay);
    }
}
