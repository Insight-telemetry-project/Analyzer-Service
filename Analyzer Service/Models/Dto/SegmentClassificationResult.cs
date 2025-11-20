namespace Analyzer_Service.Models.Dto
{
    public class SegmentClassificationResult
    {
        public SegmentBoundary Segment { get; }
        public string Label { get; }

        public SegmentClassificationResult(SegmentBoundary segment, string label)
        {
            Segment = segment;
            Label = label;
        }
    }
}
