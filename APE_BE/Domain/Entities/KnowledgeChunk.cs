using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Entities;

public class KnowledgeChunk
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonRepresentation(BsonType.ObjectId)]
    public string DocumentId { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string CourseId { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = null!;

    public int ChunkIndex { get; set; }
    public string ChunkingStrategy { get; set; } = "logical_block";
    public string ChunkType { get; set; } = "content";
    public string? SectionTitle { get; set; }
    public string? ChapterKey { get; set; }
    public string? ChapterTitle { get; set; }
    public int? ChapterOrder { get; set; }
    public List<string> SectionPath { get; set; } = new();
    public int? SectionLevel { get; set; }

    public string SourceType { get; set; } = "text";
    public string? SourceName { get; set; }
    public int? SourcePageFrom { get; set; }
    public int? SourcePageTo { get; set; }

    public string Language { get; set; } = "en";
    public string? SubjectCode { get; set; }

    public List<string> TopicTags { get; set; } = new();
    public List<string> QuestionTypeAffinity { get; set; } = new();

    public string RawText { get; set; } = null!;
    public string? NormalizedText { get; set; }
    public string? MarkdownText { get; set; }

    public List<double> Embedding { get; set; } = new();
    public string? EmbeddingProvider { get; set; }
    public string? EmbeddingModel { get; set; }
    public int? EmbeddingDim { get; set; }
    public string? TaggingProvider { get; set; }
    public string? TaggingModel { get; set; }

    public int WordCount { get; set; }
    public int TokenCount { get; set; }
    public int CharCount { get; set; }

    public bool RetrievalEnabled { get; set; } = true;
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [BsonIgnore]
    public int ChunkOrder
    {
        get => ChunkIndex;
        set => ChunkIndex = value;
    }

    [BsonIgnore]
    public int SequenceOrder
    {
        get => ChunkIndex;
        set => ChunkIndex = value;
    }

    [BsonIgnore]
    public string ContentText
    {
        get => RawText;
        set => RawText = value;
    }

    [BsonIgnore]
    public List<double> VectorEmbedding
    {
        get => Embedding;
        set => Embedding = value;
    }
}
