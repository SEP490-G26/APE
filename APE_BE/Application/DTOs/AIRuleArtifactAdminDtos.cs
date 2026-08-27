using System.Text.Json;

namespace Application.DTOs;

public class AIRuleArtifactAdminScopeDto
{
    public string? SubjectCode { get; set; }
    public string? QuestionType { get; set; }
    public string? Language { get; set; }
}

public class AIRuleArtifactVersionAdminDto
{
    public string Id { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string ChangeReason { get; set; } = string.Empty;
    public List<string> ChangeNotes { get; set; } = new();
    public JsonElement Content { get; set; }
    public string Source { get; set; } = "db";
    public string? SourcePath { get; set; }
    public string? ContentHash { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public string? ActivatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ActivatedAt { get; set; }
}

public class AIRuleArtifactAdminDto
{
    public string Id { get; set; } = string.Empty;
    public string ArtifactType { get; set; } = string.Empty;
    public string ArtifactKey { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public AIRuleArtifactAdminScopeDto Scope { get; set; } = new();
    public List<AIRuleArtifactVersionAdminDto> Versions { get; set; } = new();
    public AIRuleArtifactVersionAdminDto? ActiveVersion { get; set; }
    public int TotalVersions { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public class CreateAIRuleArtifactRequestDto
{
    public string ArtifactType { get; set; } = string.Empty;
    public string ArtifactKey { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Summary { get; set; }
    public AIRuleArtifactAdminScopeDto Scope { get; set; } = new();
    public string ContentJson { get; set; } = "{}";
    public string? ChangeReason { get; set; }
    public List<string> ChangeNotes { get; set; } = new();
    public bool ActivateNow { get; set; } = true;
}

public class CreateAIRuleArtifactVersionRequestDto
{
    public string? BaseVersion { get; set; }
    public string ContentJson { get; set; } = "{}";
    public string? ChangeReason { get; set; }
    public List<string> ChangeNotes { get; set; } = new();
    public bool ActivateNow { get; set; } = true;
}
