
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using System.Globalization;

namespace DDEyC_API.Models
{
public class JobListing
    {
        [BsonElement("id")]
        public int JobId { get; set; }

        [BsonElement("title")]
        public string Title { get; set; }

        [BsonElement("description")]
        public string Description { get; set; }
         [BsonElement("location")]
        public string Location { get; set; }

        [BsonElement("employment_type")]
        public string EmploymentType { get; set; }

        [BsonElement("company_name")]
        public string CompanyName { get; set; }

        [BsonElement("url")]
        public string Url { get; set; }

        [BsonElement("country")]
        public string Country { get; set; }

        [BsonElement("salary")]
        public string Salary { get; set; }

        [BsonElement("job_functions")]
        public List<string> JobFunctions { get; set; } = new();

        [BsonElement("created")]
        public DateTime Created { get; set; }
        
        [BsonElement("academic_levels")]
        [BsonRepresentation(BsonType.Int32)]
        public List<int> AcademicLevels { get; set; } = new();

        [BsonElement("minimum_academic_level")]
        [BsonRepresentation(BsonType.Int32)]
        public int MinimumAcademicLevel { get; set; }
    }

    public class CustomDateTimeSerializer : MongoDB.Bson.Serialization.Serializers.DateTimeSerializer
    {
        public override DateTime Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var bsonReader = context.Reader;
            var stringValue = bsonReader.ReadString();

            if (DateTime.TryParseExact(stringValue, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
            {
                return result;
            }

            // If the custom format fails, fall back to the default parsing
            return DateTime.Parse(stringValue);
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, DateTime value)
        {
            var bsonWriter = context.Writer;
            bsonWriter.WriteString(value.ToString("yyyy-MM-dd HH:mm:ss"));
        }
    }
}