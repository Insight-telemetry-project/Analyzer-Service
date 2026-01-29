namespace Analyzer_Service.Models.Dto
{
    public class SegmentFeatures
    {
        public double DurationSeconds { get; set; }
        public double MeanZ { get; set; }
        public double StdZ { get; set; }
        public double MinZ { get; set; }
        public double MaxZ { get; set; }
        public double RangeZ { get; set; }
        public double EnergyZ { get; set; }
        public double Slope { get; set; }
        public int PeakCount { get; set; }
        public int TroughCount { get; set; }
        public double MeanPrev { get; set; }
        public double MeanNext { get; set; }
    }
}
