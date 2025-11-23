namespace Analyzer_Service.Models.Dto
{
    public class SimilarityAnomalieRequest
    {
        public int MasterIndex { get; set; }
        public string FieldName { get; set; }
        public double Threshold { get; set; } = 0.75;
    }
}
