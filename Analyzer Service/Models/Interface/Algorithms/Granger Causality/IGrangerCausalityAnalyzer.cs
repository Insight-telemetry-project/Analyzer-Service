namespace Analyzer_Service.Models.Interface.Algorithms
{
    public interface IGrangerCausalityAnalyzer
    {
        double ComputeCausality(List<double> xSeries, List<double> ySeries, int lagCount);
    }
}
