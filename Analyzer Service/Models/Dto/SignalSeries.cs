namespace Analyzer_Service.Models.Dto
{
    public class SignalSeries
    {
        public List<double> Time { get; set; }
        public List<double> Values { get; set; }

        public SignalSeries(List<double> time, List<double> values)
        {
            Time = time;
            Values = values;
        }
    }
}
