using Ape.AiModule.Infrastructure;
using Ape.AiModule.Api.Services;
using DotNetEnv;
using Microsoft.AspNetCore.StaticFiles;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

var envFilePath = Path.Combine(builder.Environment.ContentRootPath, ".env");
if (File.Exists(envFilePath))
{
    Env.Load(envFilePath);
    builder.Configuration.AddEnvironmentVariables();
}

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddAiModuleInfrastructure(builder.Configuration);
builder.Services.AddSingleton<PrototypeFileIngestService>();

var app = builder.Build();

BootstrapAppData(app.Environment);

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        context.Context.Response.Headers.Pragma = "no-cache";
        context.Context.Response.Headers.Expires = "0";
    }
});
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();

static void BootstrapAppData(IWebHostEnvironment environment)
{
    var appDataRoot = Path.Combine(environment.ContentRootPath, "App_Data");
    Directory.CreateDirectory(appDataRoot);

    var runtimeFolders = new[]
    {
        Path.Combine(appDataRoot, "run-history"),
        Path.Combine(appDataRoot, "uploads"),
        Path.Combine(appDataRoot, "extraction-drafts"),
        Path.Combine(appDataRoot, "context-packs"),
        Path.Combine(appDataRoot, "benchmark-sessions")
    };

    foreach (var folder in runtimeFolders)
    {
        Directory.CreateDirectory(folder);
    }

    var requiredFiles = new[]
    {
        Path.Combine(appDataRoot, "ai-prompts.json"),
        Path.Combine(appDataRoot, "gatekeeper-policy.json"),
        Path.Combine(appDataRoot, "extracted-content-policy.json"),
        Path.Combine(appDataRoot, "embedding-tagging-policy.json"),
        Path.Combine(appDataRoot, "code-mentor-policy.json"),
        Path.Combine(appDataRoot, "ai-rubrics", "GATEKEEPER.json"),
        Path.Combine(appDataRoot, "ai-rubrics", "EXTRACTED_CONTENT.json"),
        Path.Combine(appDataRoot, "ai-rubrics", "EMBEDDING_TAGGING.json"),
        Path.Combine(appDataRoot, "ai-rubrics", "CODE_MENTOR.json"),
        Path.Combine(appDataRoot, "ai-rubrics", "C_FE.json"),
        Path.Combine(appDataRoot, "ai-rubrics", "C_PE.json"),
        Path.Combine(appDataRoot, "ai-rubrics", "DSA_JAVA_FE.json"),
        Path.Combine(appDataRoot, "ai-rubrics", "DSA_JAVA_PE.json"),
        Path.Combine(appDataRoot, "ai-rubrics", "JAVA_OOP_FE.json"),
        Path.Combine(appDataRoot, "ai-rubrics", "JAVA_OOP_PE.json"),
        Path.Combine(appDataRoot, "ai-groundtruth", "GATEKEEPER_CORE.json"),
        Path.Combine(appDataRoot, "ai-groundtruth", "EXTRACTED_CONTENT_CORE.json"),
        Path.Combine(appDataRoot, "ai-groundtruth", "EMBEDDING_TAGGING_CORE.json"),
        Path.Combine(appDataRoot, "ai-groundtruth", "CODE_MENTOR_CORE.json"),
        Path.Combine(appDataRoot, "ai-groundtruth", "QGEN_C_FE.json"),
        Path.Combine(appDataRoot, "ai-groundtruth", "QGEN_C_PE.json"),
        Path.Combine(appDataRoot, "ai-groundtruth", "QGEN_DSA_JAVA_FE.json"),
        Path.Combine(appDataRoot, "ai-groundtruth", "QGEN_DSA_JAVA_PE.json"),
        Path.Combine(appDataRoot, "ai-groundtruth", "QGEN_JAVA_OOP_FE.json"),
        Path.Combine(appDataRoot, "ai-groundtruth", "QGEN_JAVA_OOP_PE.json")
    };

    var missingFiles = requiredFiles.Where(path => !File.Exists(path)).ToList();
    if (missingFiles.Count == 0)
    {
        return;
    }

    var relativeMissingFiles = missingFiles
        .Select(path => Path.GetRelativePath(environment.ContentRootPath, path))
        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

    throw new InvalidOperationException(
        "Missing required AI module source/config files. Restore these files from git before running the prototype:"
        + Environment.NewLine
        + string.Join(Environment.NewLine, relativeMissingFiles));
}
