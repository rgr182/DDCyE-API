using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DDEyC_API.Models.DTOs{
    public class JobListingUpdateDTO
{
    [BsonElement("id")]
    public int JobId { get; set; }

    [BsonElement("description")]
    public string Description { get; set; }
}
}