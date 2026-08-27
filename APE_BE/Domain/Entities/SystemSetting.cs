using MongoDB.Bson.Serialization.Attributes;
using Domain.Enums;
using MongoDB.Bson;

namespace Domain.Entities;

public class SystemSetting
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = null!; // SETTING_GAMIFICATION, ...

    public string SettingName { get; set; } = null!;
    public object? SettingData { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string UpdatedBy { get; set; } = null!;
    public DateTime LastUpdated { get; set; }
}
