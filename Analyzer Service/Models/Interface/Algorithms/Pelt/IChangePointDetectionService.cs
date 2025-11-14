namespace Analyzer_Service.Models.Interface.Algorithms.Pelt
{
    public interface IChangePointDetectionService
    {
        Task<List<int>> DetectChangePointsAsync(
        int masterIndex,
        string fieldName);
    }
}