using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Analyzer_Service.Models.Schema
{
    public class HistoricalSimilarityPoint
    {
        [BsonElement("RecordId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string RecordId { get; set; } = string.Empty;

        [BsonElement("ComparedFlightIndex")]
        public int ComparedFlightIndex { get; set; }

        [BsonElement("StartIndex")]
        public int StartIndex { get; set; }

        [BsonElement("EndIndex")]
        public int EndIndex { get; set; }

        [BsonElement("Label")]
        public string Label { get; set; } = string.Empty;

        [BsonElement("FinalScore")]
        public double FinalScore { get; set; }
    }
}
