using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Analyzer_Service.Models.Schema
{
    [BsonIgnoreExtraElements]
    public class TelemetryFlightData
    {

        [BsonElement("Master Index")]
        public int MasterIndex { get; set; }

        [BsonElement("Fields")]
        public Dictionary<string, int> Fields { get; set; } = new();
        [BsonElement("Connections")]
        public Dictionary<string, List<string>> Connections { get; set; } = new();

        [BsonElement("Anomalies")]
        public Dictionary<string, List<double>> Anomalies { get; set; } = new();

    }
}
