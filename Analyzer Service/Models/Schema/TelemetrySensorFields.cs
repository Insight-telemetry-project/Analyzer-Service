using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Analyzer_Service.Models.Schema
{
    [BsonIgnoreExtraElements]
    public class TelemetrySensorFields
    {

        [BsonElement("Fields")]
        public Dictionary<string, double> Fields { get; set; } = new();

        [BsonElement("timestep")]
        public int Timestep { get; set; }

        [BsonElement("Master Index")]
        public int MasterIndex { get; set; }
        public Dictionary<string, List<string>> Connections { get; internal set; }
    }
}
