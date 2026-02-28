namespace Analyzer_Service.Models.Dto
{
    public class CachedFlight
    {
        public int MasterIndex { get; set; }
        public List<double> Time { get; set; }
        public Dictionary<string, List<double>> ValuesByParameter { get; set; }

        public CachedFlight(int masterIndex)
        {
            this.MasterIndex = masterIndex;
            this.Time = new List<double>();
            this.ValuesByParameter = new Dictionary<string, List<double>>();
        }
    }
}
