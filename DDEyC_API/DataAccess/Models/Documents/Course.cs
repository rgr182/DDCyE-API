using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DDEyC_API.Models
{  
    public class Course
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("title")]
        public string Title { get; set; }

        [BsonElement("location")]
        public string Location { get; set; }

        [BsonElement("date")]
        public DateTime Date { get; set; }

        [BsonElement("description")]
        public string Description { get; set; }

        [BsonElement("detail_link")]
        public string DetailLink { get; set; }

        [BsonElement("is_active")]
        public bool IsActive { get; set; }

        // Normalized fields for search
        [BsonElement("normalized_title")]
        public string NormalizedTitle { get; set; }

        [BsonElement("normalized_description")]
        public string NormalizedDescription { get; set; }
    }
}