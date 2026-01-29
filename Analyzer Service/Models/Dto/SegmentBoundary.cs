namespace Analyzer_Service.Models.Dto
{
    public class SegmentBoundary
    {
        public int StartIndex { get; }
        public int EndIndex { get; }

        public SegmentBoundary(int startIndex, int endIndex)
        {
            StartIndex = startIndex;
            EndIndex = endIndex;
        }
    }
}
