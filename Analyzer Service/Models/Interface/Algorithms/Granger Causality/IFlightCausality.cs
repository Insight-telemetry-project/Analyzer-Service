namespace Analyzer_Service.Models.Interface.Algorithms
{
    public interface IFlightCausality
    {
        Task<object> AnalyzeGrangerAsync(int masterIndex, string xField, string yField, int lag);
        Task<object> AnalyzeCcmAsync(int masterIndex, string xField, string yField, int embeddingDim, int delay);
    }
}
