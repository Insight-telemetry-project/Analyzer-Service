using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Analyzer_Service.Models.Schema
{
    public class TelemetryFieldsRecord
    {

        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;


        [BsonElement("Fields")]
        public Dictionary<string, double> Fields { get; set; } = new();

        [BsonElement("timestep")]
        public int Timestep { get; set; }

        [BsonElement("Master Index")]
        public int MasterIndex { get; set; }
    }
}
