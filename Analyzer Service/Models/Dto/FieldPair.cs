namespace Analyzer_Service.Models.Dto
{
    public class FieldPair
    {
        public string SourceField { get; }
        public string TargetField { get; }

        public FieldPair(string sourceField, string targetField)
        {
            SourceField = sourceField;
            TargetField = targetField;
        }
    }
}
