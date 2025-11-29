namespace Analyzer_Service.Models.Dto
{
    public class SegmentAnalysisResult
    {
        //public int MasterIndex { get; set; }
        //public string FieldName { get; set; }

        //public List<double> TimeSeries { get; set; }
        //public List<double> Signal { get; set; }
        //public List<double> ProcessedSignal { get; set; }

        public List<SegmentClassificationResult> Segments { get; set; }
        public List<SegmentBoundary> SegmentBoundaries { get; set; }

        //public List<Dictionary<string, double>> FeatureList { get; set; }

        public List<int> AnomalyIndexes { get; set; }
    }
}
