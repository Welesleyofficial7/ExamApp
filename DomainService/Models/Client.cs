namespace ExamApp.Models;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class Client
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonElement("first_name")]
    public string FirstName { get; set; }

    [BsonElement("last_name")]
    public string LastName { get; set; }

    [BsonElement("address")]
    public string Address { get; set; }

    [BsonElement("phone")]
    public string Phone { get; set; }
}

