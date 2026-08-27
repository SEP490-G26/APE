namespace Application.Services;

public class DocumentProcessingJob
{
    public string DocumentId { get; set; } = null!;
    public string FilePath { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string UserId { get; set; } = null!;
}