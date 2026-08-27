namespace Application.Options;

public class AzureBlobStorageOptions
{
    public const string SectionName = "AzureBlobStorage";

    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "ape-documents";
    public string DocumentsPrefix { get; set; } = "uploads/documents";
}
