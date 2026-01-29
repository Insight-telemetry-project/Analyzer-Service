namespace Analyzer_Service.Models.Dto
{
    public class SegmentAnalysisResult
    {
        public List<SegmentClassificationResult> Segments { get; set; }
        public List<SegmentBoundary> SegmentBoundaries { get; set; }
        public List<int> AnomalyIndexes { get; set; }
    }
}
