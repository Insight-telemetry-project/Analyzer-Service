using Analyzer_Service.Models.Dto;

namespace Analyzer_Service.Models.Interface.Algorithms.Clustering
{
    public interface ISegmentClassificationService
    {
        Task<List<SegmentClassificationResult>> ClassifyAsync(int masterIndex, string fieldName);
    }
}
