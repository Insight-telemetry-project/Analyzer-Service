namespace Analyzer_Service.Models.Dto
{
    public class FieldValue
    {
        public string FieldName { get; }
        public double Value { get; }

        public FieldValue(string fieldName, double value)
        {
            FieldName = fieldName;
            Value = value;
        }
    }
}
