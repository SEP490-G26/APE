using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Entities;

[BsonIgnoreExtraElements]
public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
        = ObjectId.GenerateNewId().ToString();

    public string? GoogleId { get; set; }

    public string Email { get; set; } = null!;

    public string Status { get; set; } = "Active";

    public string FullName { get; set; } = null!;

    public string? AvatarUrl { get; set; }

    public string Role { get; set; } = "Student";
    public int ExpPoints { get; set; } = 0;

    [BsonDefaultValue(0L)]
    public long AiWalletBalanceVnd { get; set; } = 0;

    public int CurrentStreak { get; set; } = 0;
    public int HighestStreak { get; set; } = 0;
    public DateTime? LastLoginDate { get; set; }
    public DateTime? LastPracticeDate { get; set; }

    public List<string> Badges { get; set; } = new();

    public List<RefreshToken> RefreshTokens { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class RefreshToken
{
    public string Token { get; set; } = null!;

    public DateTime Expires { get; set; }

    public DateTime Created { get; set; } = DateTime.UtcNow;

    public string? CreatedByIp { get; set; }

    [BsonIgnore]
    public bool IsExpired => DateTime.UtcNow >= Expires;

    [BsonIgnore]
    public bool IsActive => !IsExpired;
}
