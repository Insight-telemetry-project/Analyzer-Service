using Analyzer_Service.Models.Dto;

namespace Analyzer_Service.Models.Interface.Algorithms.Clustering
{
    public interface ISegmentClassificationService
    {

        public Task<SegmentAnalysisResult> ClassifyWithAnomaliesAsync(int masterIndex, string fieldName, int startIndex, int endIndex);
    }
}
