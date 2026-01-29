using Analyzer_Service.Models.Dto;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Analyzer_Service.Models.Schema
{
    [BsonIgnoreExtraElements]
    public class HistoricalAnomalyRecord
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("MasterIndex")]
        public int MasterIndex { get; set; }

        [BsonElement("ParameterName")]
        public string ParameterName { get; set; }

        [BsonElement("StartIndex")]
        public int StartIndex { get; set; }

        [BsonElement("EndIndex")]
        public int EndIndex { get; set; }

        [BsonElement("Label")]
        public string Label { get; set; }

        [BsonElement("PatternHash")]
        public string PatternHash { get; set; }

        [BsonElement("FeatureValues")]
        public SegmentFeatures FeatureValues { get; set; } = new SegmentFeatures();


        [BsonElement("CreatedAt")]
        public DateTime CreatedAt { get; set; }
    }
}
