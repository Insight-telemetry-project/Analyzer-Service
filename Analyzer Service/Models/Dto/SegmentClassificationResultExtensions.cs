namespace Analyzer_Service.Models.Dto
{
    public static class SegmentClassificationResultExtensions
    {
        public static int GetFlightEndIndex(this SegmentAnalysisResult segment,int count) 
        {
            return segment.Segments[segment.Segments.Count - 1].Segment.EndIndex;
        }
    }
}
