using Domain.Exceptions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Entities;

public sealed class PEQuestion
{
    private const int CLanguageId = 50;
    private const int JavaLanguageId = 62;

    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } =
        ObjectId.GenerateNewId().ToString();

    [BsonRepresentation(BsonType.ObjectId)]
    public string CourseId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public List<string> SourceDocumentIds { get; set; } = [];

    [BsonElement("ChunkIds")]
    [BsonRepresentation(BsonType.ObjectId)]
    public List<string> LegacyChunkIds { get; set; } = [];

    public List<string> TopicTags { get; set; } = [];

    public string Difficulty { get; set; } = "Medium";

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<CodeFile> SkeletonCode { get; set; } = [];

    public List<CodeFile> SolutionCode { get; set; } = [];

    public List<TestCase> TestCases { get; set; } = [];

    public List<Hint> Hints { get; set; } = [];

    public List<int> AllowedLanguageIds { get; set; } = [50];

    public int DefaultLanguageId { get; set; } = 50;

    public string Status { get; set; } = "Draft";

    public bool IsPublic { get; set; } = true;

    public string Source { get; set; } = "Admin";

    public string SourceScope { get; set; } = "SYSTEM";

    [BsonRepresentation(BsonType.ObjectId)]
    public string? OwnerUserId { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } =
        DateTime.UtcNow;

    public string? QuestionFingerprint { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? LastModifiedBy { get; set; }

    public DateTime? LastModifiedAt { get; set; }

    public string? DisabledReason { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? DisabledBy { get; set; }

    public DateTime? DisabledAt { get; set; }

    public int ResolveLanguageId(
        int? requestedLanguageId)
    {
        var allowedLanguages = AllowedLanguageIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        var inferredLanguageId =
            InferLanguageIdFromCodeArtifacts();

        if (allowedLanguages.Count == 0 &&
            inferredLanguageId.HasValue)
        {
            allowedLanguages.Add(
                inferredLanguageId.Value);
        }

        if (allowedLanguages.Count == 0)
        {
            throw new DomainRuleException(
                "Question has no configured programming language.");
        }

        if (allowedLanguages.Count == 1)
        {
            if (inferredLanguageId.HasValue &&
                inferredLanguageId.Value !=
                allowedLanguages[0] &&
                DefaultLanguageId == allowedLanguages[0])
            {
                return inferredLanguageId.Value;
            }

            return allowedLanguages[0];
        }

        if (!requestedLanguageId.HasValue)
        {
            if (inferredLanguageId.HasValue &&
                allowedLanguages.Contains(
                    inferredLanguageId.Value))
            {
                return inferredLanguageId.Value;
            }

            if (!allowedLanguages.Contains(DefaultLanguageId))
            {
                throw new DomainRuleException(
                    "Default language configuration is invalid.");
            }

            return DefaultLanguageId;
        }

        if (!allowedLanguages.Contains(
                requestedLanguageId.Value))
        {
            throw new DomainRuleException(
                $"LanguageId {requestedLanguageId.Value} " +
                "is not allowed for this question.");
        }

        return requestedLanguageId.Value;
    }

    private int? InferLanguageIdFromCodeArtifacts()
    {
        static int? MapFileNameToLanguageId(string? filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                return null;
            }

            var normalized = filename.Trim();

            if (normalized.EndsWith(
                    ".java",
                    StringComparison.OrdinalIgnoreCase))
            {
                return JavaLanguageId;
            }

            if (normalized.EndsWith(
                    ".c",
                    StringComparison.OrdinalIgnoreCase))
            {
                return CLanguageId;
            }

            return null;
        }

        var skeletonLanguage = SkeletonCode
            .Select(file => MapFileNameToLanguageId(file?.Filename))
            .FirstOrDefault(languageId => languageId.HasValue);

        if (skeletonLanguage.HasValue)
        {
            return skeletonLanguage.Value;
        }

        var solutionLanguage = SolutionCode
            .Select(file => MapFileNameToLanguageId(file?.Filename))
            .FirstOrDefault(languageId => languageId.HasValue);

        return solutionLanguage;
    }
}
