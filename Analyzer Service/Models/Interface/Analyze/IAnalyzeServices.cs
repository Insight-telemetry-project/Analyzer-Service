namespace Analyzer_Service.Models.Interface.Analyze
{
    public interface IAnalyzeServices
    {
        Task<List<long>> Analyze(int flightId);
        Task AnalyzeFlightHistoryById(int flightId);

    }
}
