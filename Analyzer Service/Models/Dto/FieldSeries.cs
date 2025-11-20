namespace Analyzer_Service.Models.Dto
{
    public class FieldSeries
    {
        public string FieldName { get; }
        public List<double> Values { get; }

        public FieldSeries(string fieldName, List<double> values)
        {
            FieldName = fieldName;
            Values = values;
        }
    }
}
