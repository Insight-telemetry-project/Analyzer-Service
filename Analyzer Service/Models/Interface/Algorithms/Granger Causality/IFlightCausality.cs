namespace Analyzer_Service.Models.Interface.Algorithms
{
    public interface IFlightCausality
    {
        Task<object> AnalyzeAutoAsync(int masterIndex, string xField, string yField, int lag, int embeddingDim, int delay);
    }
}
