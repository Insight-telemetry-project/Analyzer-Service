namespace Analyzer_Service.Models.Dto
{
    public class SegmentClassificationResult
    {
        public SegmentBoundary Segment { get; }
        public string Label { get; }
        public Dictionary<string, double> FeatureValues { get; set; } = new();
        public double[] HashVector { get; set; } = Array.Empty<double>();
        public SegmentClassificationResult(SegmentBoundary segment, string label)
        {
            Segment = segment;
            Label = label;
        }
    }
}
