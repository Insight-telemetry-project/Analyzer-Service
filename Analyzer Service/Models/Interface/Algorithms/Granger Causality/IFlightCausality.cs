namespace Analyzer_Service.Models.Interface.Algorithms
{
    public interface IFlightCausality
    {
        Task<object> AnalyzeCausalityAsync(int masterIndex, string xField, string yField, int lag);
    }
}
