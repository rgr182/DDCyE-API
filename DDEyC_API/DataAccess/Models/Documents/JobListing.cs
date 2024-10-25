using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using System.Globalization;

namespace DDEyC_API.Models
{
    public class JobListing
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("id")]
        public int JobId { get; set; }

        [BsonElement("created")]
        [BsonSerializer(typeof(CustomDateTimeSerializer))]
        public DateTime Created { get; set; }

        [BsonElement("last_updated")]
        [BsonSerializer(typeof(CustomDateTimeSerializer))]
        public DateTime LastUpdated { get; set; }

        [BsonElement("time_posted")]
        public string TimePosted { get; set; }

        [BsonElement("title")]
        public string Title { get; set; }

        [BsonElement("description")]
        public string Description { get; set; }

        [BsonElement("seniority")]
        public string Seniority { get; set; }

        [BsonElement("employment_type")]
        public string EmploymentType { get; set; }

        [BsonElement("location")]
        public string Location { get; set; }

        [BsonElement("url")]
        public string Url { get; set; }

        [BsonElement("hash")]
        public string Hash { get; set; }

        [BsonElement("company_id")]
        public long? CompanyId { get; set; }

        [BsonElement("company_name")]
        public string CompanyName { get; set; }

        [BsonElement("company_url")]
        public string? CompanyUrl { get; set; }

        [BsonElement("external_url")]
        public string? ExternalUrl { get; set; }

        [BsonElement("deleted")]
        public int Deleted { get; set; }

        [BsonElement("application_active")]
        public int ApplicationActive { get; set; }

        [BsonElement("salary")]
        public string? Salary { get; set; }

        [BsonElement("applicants_count")]
        public string? ApplicantsCount { get; set; }

        [BsonElement("linkedin_job_id")]
        [BsonRepresentation(BsonType.Int64)]
        public long LinkedinJobId { get; set; }

        [BsonElement("country")]
        public string Country { get; set; }

        [BsonElement("redirected_url")]
        public string RedirectedUrl { get; set; }

        [BsonElement("job_functions_collection")]
        public List<string> JobFunctions { get; set; }

        [BsonElement("job_industry_collection")]
        public List<JobIndustry> JobIndustries { get; set; }
    }

    public class JobIndustry
    {
        [BsonElement("job_industry_list")]
        public JobIndustryList JobIndustryList { get; set; }
    }

    public class JobIndustryList
    {
        [BsonElement("industry")]
        public string Industry { get; set; }
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