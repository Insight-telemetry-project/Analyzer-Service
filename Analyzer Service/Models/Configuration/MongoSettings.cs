namespace Analyzer_Service.Models.Configuration
{
    public class MongoSettings
    {
        public const string SectionName = "MongoSettings";

        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public string CollectionTelemetryFields { get; set; } = string.Empty;
        public string CollectionTelemetryFlightData { get; set; } = string.Empty;
        public string CollectionHistoricalAnomalies { get; set; } = string.Empty;

    }
}
