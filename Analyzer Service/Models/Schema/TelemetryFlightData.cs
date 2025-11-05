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
    }
}
