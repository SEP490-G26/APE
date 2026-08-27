namespace Domain.Entities;

public class DocumentChapterSummary
{
    public string ChapterKey { get; set; } = string.Empty;
    public string ChapterTitle { get; set; } = string.Empty;
    public int? ChapterOrder { get; set; }
    public int ChunkCount { get; set; }
    public int EstimatedTokens { get; set; }
    public List<string> CoveredTopics { get; set; } = new();
    public List<string> SampleSectionTitles { get; set; } = new();
    public string? OverviewShort { get; set; }
}
