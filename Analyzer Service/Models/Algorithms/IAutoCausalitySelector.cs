using Analyzer_Service.Models.Ro.Algorithms;

namespace Analyzer_Service.Models.Algorithms
{
    public interface IAutoCausalitySelector
    {
        CausalitySelectionResult SelectAlgorithm(List<double> sourceSeries, List<double> targetSeries);
    }
}
