namespace Analyzer_Service.Models.Dto
{
    public class ParameterSeries
    {
        public string ParameterName { get; }
        public List<double> Values { get; }

        public ParameterSeries(string parameterName, List<double> values)
        {
            ParameterName = parameterName;
            Values = values;
        }
    }
}
