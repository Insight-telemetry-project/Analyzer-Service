namespace Analyzer_Service.Models.Interface.Algorithms
{
    public interface IFlightCausality
    {
        Task<object> AnalyzeFlightAsync(int masterIndex, int lag, int embeddingDim, int delay);

    }
}
