namespace API.Workers;

// Legacy worker kept only for source compatibility.
// Document ingestion is now handled synchronously in DocumentService.
public class DocumentProcessingWorker : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
