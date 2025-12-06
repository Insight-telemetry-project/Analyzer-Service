namespace Analyzer_Service.Models.Interface.Algorithms
{
    public interface IGrangerCausalityAnalyzer
    {
        public double ComputeCausality(List<double> xSeries, List<double> ySeries, int lagCount);
    }
}
