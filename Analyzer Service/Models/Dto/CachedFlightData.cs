namespace Analyzer_Service.Models.Dto
{
    public class CachedFlightData
    {
        public Dictionary<string, List<double>> FieldValues { get; }

        public CachedFlightData(Dictionary<string, List<double>> fieldValues)
        {
            FieldValues = fieldValues;
        }
    }
}
