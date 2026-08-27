using Ape.AiModule.Infrastructure;
using Ape.AiModule.Api.Services;
using DotNetEnv;
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

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
