namespace Analyzer_Service.Models.Interface.Analyze
{
    public interface IAnalyzeServices
    {
        Task Analyze(int flightId);
        Task AnalyzeFlightHistoryById(int flightId);

    }
}
