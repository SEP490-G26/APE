using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Application.Common;
using Application.DTOs;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace API.Controllers;

[ApiController]
[Route("api/admin/ai/rule-artifacts")]
[Authorize(Roles = "Admin")]
public class AIRuleArtifactAdminController : ControllerBase
{
    private readonly DbContext _dbContext;

    public AIRuleArtifactAdminController(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<AIRuleArtifactAdminDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var items = await _dbContext.AIRuleArtifacts
            .Find(Builders<AIRuleArtifact>.Filter.Empty)
            .ToListAsync(cancellationToken);

        var result = items
            .GroupBy(BuildArtifactIdentity, StringComparer.OrdinalIgnoreCase)
            .Select(MapArtifactGroup)
            .OrderBy(item => item.ArtifactType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ArtifactKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AIRuleArtifactAdminDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CreateAIRuleArtifactRequestDto dto, CancellationToken cancellationToken)
    {
        ValidateContentJson(dto.ContentJson);

        var normalizedScope = NormalizeScope(dto.Scope);
        var existing = await _dbContext.AIRuleArtifacts
            .Find(BuildArtifactGroupFilter(dto.ArtifactType, dto.ArtifactKey, normalizedScope))
            .AnyAsync(cancellationToken);

        if (existing)
        {
            return Conflict(ApiResponse.Fail("Artifact already exists for the selected type and scope."));
        }

        var now = DateTime.UtcNow;
        var artifact = new AIRuleArtifact
        {
            ArtifactType = dto.ArtifactType,
            ArtifactKey = dto.ArtifactKey,
            Scope = normalizedScope,
            Version = "v1",
            IsActive = dto.ActivateNow,
            ContentJson = dto.ContentJson,
            Description = NormalizeNullable(dto.Description),
            Summary = NormalizeNullable(dto.Summary),
            ChangeReason = NormalizeNullable(dto.ChangeReason),
            ChangeNotes = NormalizeNotes(dto.ChangeNotes),
            Source = "db",
            SourcePath = dto.ArtifactKey,
            ContentHash = AIRuleArtifactStore.ComputeContentHash(dto.ContentJson),
            CreatedBy = UserId,
            UpdatedBy = UserId,
            ActivatedBy = dto.ActivateNow ? UserId : null,
            CreatedAt = now,
            UpdatedAt = now,
            ActivatedAt = dto.ActivateNow ? now : null
        };

        if (dto.ActivateNow)
        {
            await AIRuleArtifactStore.ActivateAsync(_dbContext, artifact, cancellationToken);
        }
        else
        {
            await AIRuleArtifactStore.UpsertAsync(_dbContext, artifact, cancellationToken);
        }

        return Ok(ApiResponse.Ok(await GetArtifactGroupAsync(artifact, cancellationToken)));
    }

    [HttpPost("{artifactId}/versions")]
    [ProducesResponseType(typeof(ApiResponse<AIRuleArtifactAdminDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateVersion(string artifactId, [FromBody] CreateAIRuleArtifactVersionRequestDto dto, CancellationToken cancellationToken)
    {
        ValidateContentJson(dto.ContentJson);

        var group = await LoadArtifactGroupAsync(artifactId, cancellationToken);
        if (group.Count == 0)
        {
            return NotFound(ApiResponse.Fail($"Artifact '{artifactId}' not found."));
        }

        var template = group[0];
        var now = DateTime.UtcNow;
        var artifact = new AIRuleArtifact
        {
            ArtifactType = template.ArtifactType,
            ArtifactKey = template.ArtifactKey,
            Scope = CloneScope(template.Scope),
            Version = BuildNextVersion(group),
            IsActive = dto.ActivateNow,
            ContentJson = dto.ContentJson,
            Description = template.Description,
            Summary = template.Summary,
            ChangeReason = NormalizeNullable(dto.ChangeReason),
            ChangeNotes = NormalizeNotes(dto.ChangeNotes),
            Source = template.Source,
            SourcePath = template.SourcePath ?? template.ArtifactKey,
            ContentHash = AIRuleArtifactStore.ComputeContentHash(dto.ContentJson),
            CreatedBy = UserId,
            UpdatedBy = UserId,
            ActivatedBy = dto.ActivateNow ? UserId : null,
            CreatedAt = now,
            UpdatedAt = now,
            ActivatedAt = dto.ActivateNow ? now : null
        };

        if (dto.ActivateNow)
        {
            await AIRuleArtifactStore.ActivateAsync(_dbContext, artifact, cancellationToken);
        }
        else
        {
            await AIRuleArtifactStore.UpsertAsync(_dbContext, artifact, cancellationToken);
        }

        return Ok(ApiResponse.Ok(await GetArtifactGroupAsync(artifact, cancellationToken)));
    }

    [HttpPost("{artifactId}/versions/{versionName}/activate")]
    [ProducesResponseType(typeof(ApiResponse<AIRuleArtifactAdminDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Activate(string artifactId, string versionName, CancellationToken cancellationToken)
    {
        var group = await LoadArtifactGroupAsync(artifactId, cancellationToken);
        var target = group.FirstOrDefault(item => string.Equals(item.Version, versionName, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            return NotFound(ApiResponse.Fail($"Version '{versionName}' not found."));
        }

        target.IsActive = true;
        target.UpdatedAt = DateTime.UtcNow;
        target.UpdatedBy = UserId;
        target.ActivatedAt = DateTime.UtcNow;
        target.ActivatedBy = UserId;

        await AIRuleArtifactStore.ActivateAsync(_dbContext, target, cancellationToken);

        return Ok(ApiResponse.Ok(await GetArtifactGroupAsync(target, cancellationToken)));
    }

    [HttpDelete("{artifactId}/versions/{versionName}")]
    [ProducesResponseType(typeof(ApiResponse<AIRuleArtifactAdminDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteVersion(string artifactId, string versionName, CancellationToken cancellationToken)
    {
        var group = await LoadArtifactGroupAsync(artifactId, cancellationToken);
        if (group.Count == 0)
        {
            return NotFound(ApiResponse.Fail($"Artifact '{artifactId}' not found."));
        }

        var target = group.FirstOrDefault(item => string.Equals(item.Version, versionName, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            return NotFound(ApiResponse.Fail($"Version '{versionName}' not found."));
        }

        if (target.IsActive)
        {
            return BadRequest(ApiResponse.Fail("Active version cannot be deleted. Activate another version first."));
        }

        await _dbContext.AIRuleArtifacts.DeleteOneAsync(Builders<AIRuleArtifact>.Filter.Eq(item => item.Id, target.Id), cancellationToken);

        var remaining = await LoadArtifactGroupAsync(artifactId, cancellationToken);
        if (remaining.Count == 0)
        {
            return Ok(ApiResponse.Ok(new AIRuleArtifactAdminDto
            {
                Id = artifactId
            }));
        }

        return Ok(ApiResponse.Ok(MapArtifactGroup(remaining)));
    }

    [HttpDelete("{artifactId}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteArtifact(string artifactId, CancellationToken cancellationToken)
    {
        var group = await LoadArtifactGroupAsync(artifactId, cancellationToken);
        if (group.Count == 0)
        {
            return NotFound(ApiResponse.Fail($"Artifact '{artifactId}' not found."));
        }

        var ids = group.Select(item => item.Id).ToList();
        await _dbContext.AIRuleArtifacts.DeleteManyAsync(
            Builders<AIRuleArtifact>.Filter.In(item => item.Id, ids),
            cancellationToken);

        return Ok(ApiResponse.Ok(new
        {
            deleted = true,
            artifactId
        }));
    }

    private string? UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    private async Task<List<AIRuleArtifact>> LoadArtifactGroupAsync(string artifactId, CancellationToken cancellationToken)
    {
        var identity = ParseArtifactIdentity(artifactId);
        return await _dbContext.AIRuleArtifacts
            .Find(BuildArtifactGroupFilter(identity.ArtifactType, identity.ArtifactKey, identity.Scope))
            .ToListAsync(cancellationToken);
    }

    private async Task<AIRuleArtifactAdminDto> GetArtifactGroupAsync(AIRuleArtifact artifact, CancellationToken cancellationToken)
    {
        var items = await _dbContext.AIRuleArtifacts
            .Find(BuildArtifactGroupFilter(artifact.ArtifactType, artifact.ArtifactKey, artifact.Scope))
            .ToListAsync(cancellationToken);

        return MapArtifactGroup(items);
    }

    private static AIRuleArtifactAdminDto MapArtifactGroup(IEnumerable<AIRuleArtifact> items)
    {
        var ordered = items
            .OrderByDescending(item => ParseVersionNumber(item.Version))
            .ThenByDescending(item => item.CreatedAt)
            .ToList();

        var first = ordered[0];
        var versions = ordered.Select(MapVersion).ToList();
        var latest = ordered
            .OrderByDescending(item => item.UpdatedAt)
            .First();

        return new AIRuleArtifactAdminDto
        {
            Id = BuildArtifactIdentity(first),
            ArtifactType = first.ArtifactType,
            ArtifactKey = first.ArtifactKey,
            Description = first.Description ?? string.Empty,
            Summary = first.Summary ?? string.Empty,
            Scope = new AIRuleArtifactAdminScopeDto
            {
                SubjectCode = first.Scope.SubjectCode,
                QuestionType = first.Scope.QuestionType,
                Language = first.Scope.Language
            },
            Versions = versions,
            ActiveVersion = versions.FirstOrDefault(item => item.IsActive),
            TotalVersions = versions.Count,
            UpdatedAt = latest.UpdatedAt,
            UpdatedBy = latest.UpdatedBy
        };
    }

    private static AIRuleArtifactVersionAdminDto MapVersion(AIRuleArtifact item)
    {
        return new AIRuleArtifactVersionAdminDto
        {
            Id = item.Id,
            Version = item.Version,
            IsActive = item.IsActive,
            ChangeReason = item.ChangeReason ?? string.Empty,
            ChangeNotes = item.ChangeNotes ?? new List<string>(),
            Content = ParseContentJson(item.ContentJson),
            Source = item.Source,
            SourcePath = item.SourcePath,
            ContentHash = item.ContentHash,
            CreatedBy = item.CreatedBy,
            UpdatedBy = item.UpdatedBy,
            ActivatedBy = item.ActivatedBy,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
            ActivatedAt = item.ActivatedAt
        };
    }

    private static AIRuleArtifactScope NormalizeScope(AIRuleArtifactAdminScopeDto? scope)
    {
        return new AIRuleArtifactScope
        {
            SubjectCode = NormalizeNullable(scope?.SubjectCode),
            QuestionType = NormalizeNullable(scope?.QuestionType),
            Language = NormalizeNullable(scope?.Language)
        };
    }

    private static AIRuleArtifactScope CloneScope(AIRuleArtifactScope scope)
    {
        return new AIRuleArtifactScope
        {
            SubjectCode = scope.SubjectCode,
            QuestionType = scope.QuestionType,
            Language = scope.Language
        };
    }

    private static FilterDefinition<AIRuleArtifact> BuildArtifactGroupFilter(string artifactType, string artifactKey, AIRuleArtifactScope scope)
    {
        return Builders<AIRuleArtifact>.Filter.Eq(item => item.ArtifactType, NormalizeRequired(artifactType)) &
               Builders<AIRuleArtifact>.Filter.Eq(item => item.ArtifactKey, NormalizeRequired(artifactKey)) &
               Builders<AIRuleArtifact>.Filter.Eq("Scope.SubjectCode", NormalizeNullable(scope.SubjectCode)) &
               Builders<AIRuleArtifact>.Filter.Eq("Scope.QuestionType", NormalizeNullable(scope.QuestionType)) &
               Builders<AIRuleArtifact>.Filter.Eq("Scope.Language", NormalizeNullable(scope.Language));
    }

    private static ArtifactIdentity ParseArtifactIdentity(string artifactId)
    {
        string decoded;
        try
        {
            var normalized = (artifactId ?? string.Empty)
                .Replace('-', '+')
                .Replace('_', '/');
            normalized = normalized.PadRight(normalized.Length + ((4 - normalized.Length % 4) % 4), '=');
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(normalized));
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Artifact id is invalid.");
        }

        var parts = decoded.Split(new[] { "::" }, StringSplitOptions.None);
        return new ArtifactIdentity
        {
            ArtifactType = parts.ElementAtOrDefault(0) ?? string.Empty,
            ArtifactKey = parts.ElementAtOrDefault(1) ?? string.Empty,
            Scope = new AIRuleArtifactScope
            {
                SubjectCode = NormalizeNullable(parts.ElementAtOrDefault(2)),
                QuestionType = NormalizeNullable(parts.ElementAtOrDefault(3)),
                Language = NormalizeNullable(parts.ElementAtOrDefault(4))
            }
        };
    }

    private static string BuildArtifactIdentity(AIRuleArtifact item)
    {
        var raw = string.Join("::",
            item.ArtifactType ?? string.Empty,
            item.ArtifactKey ?? string.Empty,
            item.Scope.SubjectCode ?? string.Empty,
            item.Scope.QuestionType ?? string.Empty,
            item.Scope.Language ?? string.Empty);

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string BuildNextVersion(IEnumerable<AIRuleArtifact> items)
    {
        var max = items
            .Select(item => ParseVersionNumber(item.Version))
            .DefaultIfEmpty(0)
            .Max();

        return $"v{max + 1}";
    }

    private static int ParseVersionNumber(string? version)
    {
        var raw = (version ?? string.Empty).Trim();
        if (raw.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            raw = raw[1..];
        }

        return int.TryParse(raw, out var parsed) ? parsed : 0;
    }

    private static JsonElement ParseContentJson(string? contentJson)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(contentJson) ? "{}" : contentJson);
        return document.RootElement.Clone();
    }

    private static void ValidateContentJson(string? contentJson)
    {
        try
        {
            using var _ = JsonDocument.Parse(string.IsNullOrWhiteSpace(contentJson) ? "{}" : contentJson);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Content JSON is not valid.");
        }
    }

    private static string NormalizeRequired(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static List<string> NormalizeNotes(IEnumerable<string>? notes)
    {
        return (notes ?? Array.Empty<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToList();
    }

    private sealed class ArtifactIdentity
    {
        public string ArtifactType { get; set; } = string.Empty;
        public string ArtifactKey { get; set; } = string.Empty;
        public AIRuleArtifactScope Scope { get; set; } = new();
    }
}
