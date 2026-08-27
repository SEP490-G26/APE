using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Infrastructure.Persistence;

public class DocumentRepository : IDocumentRepository
{
    private readonly IMongoCollection<Document> _col;
    private readonly IMongoCollection<BsonDocument> _rawCol;

    public DocumentRepository(DbContext ctx)
    {
        _col = ctx.Documents;
        _rawCol = ctx.Database.GetCollection<BsonDocument>("Documents");
    }

    public async Task CreateAsync(Document doc) =>
        await _col.InsertOneAsync(doc);

    public async Task<Document?> GetByIdAsync(string id) =>
        await _col.Find(d => d.Id == id).FirstOrDefaultAsync();

    public async Task UpdateAsync(Document doc) =>
        await _col.ReplaceOneAsync(d => d.Id == doc.Id, doc);

    public async Task<Document?> FindReusableByFileChecksumAsync(string source, string? courseId, string? userId, string fileChecksum)
    {
        var filter = BuildReusableDuplicateFilter(source, courseId, userId) &
                     Builders<Document>.Filter.Eq(d => d.FileChecksum, fileChecksum);
        return await _col.Find(filter)
            .SortByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<Document?> FindReusableByNormalizedChecksumAsync(string source, string? courseId, string? userId, string normalizedContentChecksum)
    {
        var filter = BuildReusableDuplicateFilter(source, courseId, userId) &
                     Builders<Document>.Filter.Eq(d => d.NormalizedContentChecksum, normalizedContentChecksum);
        return await _col.Find(filter)
            .SortByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<int> CountByUserAsync(string userId) =>
        (int)await _col.CountDocumentsAsync(d => d.UserId == userId && d.Source == "BYOS" && d.Status != DocumentStatus.Deleted);

    public async Task<(List<Document> Items, long Total)> ListByUserAsync(string userId, int page, int limit)
    {
        var filter = BuildIdFilter("UserId", userId) &
                     Builders<BsonDocument>.Filter.Eq("Source", "BYOS") &
                     Builders<BsonDocument>.Filter.Ne("Status", "Deleted");
        var skip = (Math.Max(1, page) - 1) * Math.Max(1, limit);
        var items = await _rawCol.Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Descending("CreatedAt"))
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
        var total = await _rawCol.CountDocumentsAsync(filter);
        return (items.Select(MapDocument).ToList(), total);
    }

    public async Task<(List<Document> Items, long Total)> ListSystemAsync(int page, int limit, string? courseId = null)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("Source", "SystemSyllabus") &
                     Builders<BsonDocument>.Filter.Ne("Status", "Deleted");

        if (!string.IsNullOrWhiteSpace(courseId))
        {
            filter &= BuildIdFilter("CourseId", courseId);
        }

        var skip = (Math.Max(1, page) - 1) * Math.Max(1, limit);
        var items = await _rawCol.Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Descending("CreatedAt"))
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
        var total = await _rawCol.CountDocumentsAsync(filter);
        return (items.Select(MapDocument).ToList(), total);
    }

    public async Task<(List<Document> Items, long Total)> ListByCourseAsync(string courseId, int page, int limit)
    {
        var filter = BuildIdFilter("CourseId", courseId) &
                     Builders<BsonDocument>.Filter.Ne("Status", "Deleted");
        var skip = (Math.Max(1, page) - 1) * Math.Max(1, limit);
        var items = await _rawCol.Find(filter)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
        var total = await _rawCol.CountDocumentsAsync(filter);
        return (items.Select(MapDocument).ToList(), total);
    }

    public async Task<(List<Document> Items, long Total)> GetPendingReviewAsync(int page, int limit)
    {
        var filter = Builders<Document>.Filter.Eq(d => d.IsActive, false);
        var skip = (Math.Max(1, page) - 1) * Math.Max(1, limit);
        var items = await _col.Find(filter).Skip(skip).Limit(limit).ToListAsync();
        var total = await _col.CountDocumentsAsync(filter);
        return (items, (long)total);
    }

    public async Task<bool> AreAllByCourseApprovedAsync(string courseId)
    {
        var total = await _col.CountDocumentsAsync(d => d.CourseId == courseId);
        if (total == 0) return false;
        var active = await _col.CountDocumentsAsync(d => d.CourseId == courseId && d.IsActive);
        return active == total;
    }

    private static FilterDefinition<Document> BuildReusableDuplicateFilter(string source, string? courseId, string? userId)
    {
        var filter = Builders<Document>.Filter.Eq(d => d.Source, source) &
                     Builders<Document>.Filter.Ne(d => d.Status, DocumentStatus.Failed) &
                     Builders<Document>.Filter.Ne(d => d.Status, DocumentStatus.Deleted);

        if (!string.IsNullOrWhiteSpace(courseId))
        {
            filter &= Builders<Document>.Filter.Eq(d => d.CourseId, courseId);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            filter &= Builders<Document>.Filter.Eq(d => d.UserId, userId);
        }

        return filter;
    }

    private static FilterDefinition<BsonDocument> BuildIdFilter(string fieldName, string id)
    {
        var stringFilter = Builders<BsonDocument>.Filter.Eq(fieldName, id);
        if (ObjectId.TryParse(id, out var objectId))
        {
            return Builders<BsonDocument>.Filter.Or(
                stringFilter,
                Builders<BsonDocument>.Filter.Eq(fieldName, objectId));
        }

        return stringFilter;
    }

    private static Document MapDocument(BsonDocument bson)
    {
        return new Document
        {
            Id = ReadId(bson, "_id"),
            CourseId = ReadId(bson, "CourseId"),
            UserId = ReadId(bson, "UserId"),
            FileName = ReadString(bson, "FileName"),
            FilePath = ReadString(bson, "FilePath"),
            FileType = ReadString(bson, "FileType"),
            FileSizeBytes = ReadInt64(bson, "FileSizeBytes"),
            FileChecksum = ReadNullableString(bson, "FileChecksum"),
            NormalizedContentChecksum = ReadNullableString(bson, "NormalizedContentChecksum"),
            Source = ReadNullableString(bson, "Source"),
            SubjectCode = ReadNullableString(bson, "SubjectCode"),
            GatekeeperVerdict = ReadNullableString(bson, "GatekeeperVerdict"),
            LastExtractionDraftId = ReadNullableId(bson, "LastExtractionDraftId"),
            ChapterSummaries = new List<DocumentChapterSummary>(),
            LastExtractedAt = ReadNullableDateTime(bson, "LastExtractedAt"),
            LastEmbeddedAt = ReadNullableDateTime(bson, "LastEmbeddedAt"),
            Status = ReadStatus(bson, "Status"),
            CreatedAt = ReadDateTime(bson, "CreatedAt"),
            IsActive = ReadBool(bson, "IsActive", defaultValue: false),
            DeletedAt = ReadNullableDateTime(bson, "DeletedAt"),
            DeletedBy = ReadNullableId(bson, "DeletedBy")
        };
    }

    private static long ReadInt64(BsonDocument bson, string fieldName)
    {
        if (!bson.TryGetValue(fieldName, out var value) || value.IsBsonNull)
        {
            return 0L;
        }

        return value.BsonType switch
        {
            BsonType.Int32 => value.AsInt32,
            BsonType.Int64 => value.AsInt64,
            BsonType.Double => (long)value.AsDouble,
            BsonType.Decimal128 => (long)value.AsDecimal128,
            _ => 0L
        };
    }

    private static string ReadId(BsonDocument bson, string fieldName)
    {
        if (!bson.TryGetValue(fieldName, out var value) || value.IsBsonNull)
        {
            return string.Empty;
        }

        return value.BsonType switch
        {
            BsonType.ObjectId => value.AsObjectId.ToString(),
            BsonType.String => value.AsString,
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string? ReadNullableId(BsonDocument bson, string fieldName)
    {
        var value = ReadId(bson, fieldName);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string ReadString(BsonDocument bson, string fieldName, string defaultValue = "")
    {
        if (!bson.TryGetValue(fieldName, out var value) || value.IsBsonNull)
        {
            return defaultValue;
        }

        return value.BsonType switch
        {
            BsonType.String => value.AsString,
            BsonType.ObjectId => value.AsObjectId.ToString(),
            _ => value.ToString() ?? defaultValue
        };
    }

    private static string? ReadNullableString(BsonDocument bson, string fieldName)
    {
        var value = ReadString(bson, fieldName);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static DateTime ReadDateTime(BsonDocument bson, string fieldName)
    {
        if (!bson.TryGetValue(fieldName, out var value) || value.IsBsonNull)
        {
            return DateTime.UtcNow;
        }

        return value.BsonType switch
        {
            BsonType.DateTime => value.ToUniversalTime(),
            BsonType.String when DateTime.TryParse(value.AsString, out var parsed) => parsed.ToUniversalTime(),
            _ => DateTime.UtcNow
        };
    }

    private static DateTime? ReadNullableDateTime(BsonDocument bson, string fieldName)
    {
        if (!bson.TryGetValue(fieldName, out var value) || value.IsBsonNull)
        {
            return null;
        }

        return value.BsonType switch
        {
            BsonType.DateTime => value.ToUniversalTime(),
            BsonType.String when DateTime.TryParse(value.AsString, out var parsed) => parsed.ToUniversalTime(),
            _ => null
        };
    }

    private static bool ReadBool(BsonDocument bson, string fieldName, bool defaultValue)
    {
        if (!bson.TryGetValue(fieldName, out var value) || value.IsBsonNull)
        {
            return defaultValue;
        }

        return value.BsonType switch
        {
            BsonType.Boolean => value.AsBoolean,
            BsonType.String when bool.TryParse(value.AsString, out var parsed) => parsed,
            BsonType.Int32 => value.AsInt32 != 0,
            BsonType.Int64 => value.AsInt64 != 0,
            _ => defaultValue
        };
    }

    private static DocumentStatus ReadStatus(BsonDocument bson, string fieldName)
    {
        if (!bson.TryGetValue(fieldName, out var value) || value.IsBsonNull)
        {
            return DocumentStatus.Processing;
        }

        if (value.BsonType == BsonType.String &&
            Enum.TryParse<DocumentStatus>(value.AsString, true, out var parsedString))
        {
            return parsedString;
        }

        if ((value.BsonType == BsonType.Int32 || value.BsonType == BsonType.Int64) &&
            Enum.IsDefined(typeof(DocumentStatus), value.ToInt32()))
        {
            return (DocumentStatus)value.ToInt32();
        }

        return DocumentStatus.Processing;
    }
}
