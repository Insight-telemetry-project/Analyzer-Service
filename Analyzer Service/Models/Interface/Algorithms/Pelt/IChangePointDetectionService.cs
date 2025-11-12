namespace Analyzer_Service.Models.Interface.Algorithms.Pelt
{
    public interface IChangePointDetectionService
    {
        Task<IReadOnlyList<int>> DetectChangePointsAsync(
        int masterIndex,
        string fieldName);
    }
}