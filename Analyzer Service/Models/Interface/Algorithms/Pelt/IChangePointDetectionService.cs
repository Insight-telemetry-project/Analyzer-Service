using Analyzer_Service.Models.Enums;

namespace Analyzer_Service.Models.Interface.Algorithms.Pelt
{
    public interface IChangePointDetectionService
    {
        public Task<List<int>> DetectChangePointsAsync(int masterIndex,string fieldName, flightStatus status);
    }
}