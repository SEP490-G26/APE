using Ape.AiModule.Application.Interfaces.AI;
using Ape.AiModule.Application.Options.AI;
using Ape.AiModule.Application.Services.AI;
using Ape.AiModule.Infrastructure.AI;
using Ape.AiModule.Infrastructure.Files;
using Ape.AiModule.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Ape.AiModule.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAiModuleInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiProviderOptions>(configuration.GetSection(AiProviderOptions.SectionName));
        services.AddHttpClient("ai-provider-openai")
            .ConfigureHttpClient((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<AiProviderOptions>>().CurrentValue.OpenAI;
                client.BaseAddress = BuildBaseUri(options.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            });
        services.AddHttpClient("ai-provider-gemini")
            .ConfigureHttpClient((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<AiProviderOptions>>().CurrentValue.Gemini;
                client.BaseAddress = BuildBaseUri(options.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            });
        services.AddHttpClient("ai-provider-cohere")
            .ConfigureHttpClient((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<AiProviderOptions>>().CurrentValue.Cohere;
                client.BaseAddress = BuildBaseUri(options.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            });

        services.AddSingleton<IModuleRepository, InMemoryModuleRepository>();
        services.AddSingleton<DemoAiProviderGateway>();
        services.AddSingleton<IAiProviderGateway, RealAiProviderGateway>();
        services.AddSingleton<IProviderCatalogService, ProviderCatalogService>();
        services.AddSingleton<IProviderConfigurationService, ProviderConfigurationService>();
        services.AddSingleton<IPromptTemplateService, FilePromptTemplateService>();
        services.AddSingleton<IAiConfigHistoryService, FileAiConfigHistoryService>();
        services.AddSingleton<IAiRunHistoryService, FileAiRunHistoryService>();
        services.AddSingleton<IAiReportSyncService, ScriptedAiReportSyncService>();
        services.AddSingleton<IBenchmarkSessionService, FileBenchmarkSessionService>();
        services.AddSingleton<IExtractionDraftStore, FileExtractionDraftStore>();
        services.AddSingleton<IGatekeeperPolicyService, FileGatekeeperPolicyService>();
        services.AddSingleton<IExtractedContentPolicyService, FileExtractedContentPolicyService>();
        services.AddSingleton<IEmbeddingTaggingPolicyService, FileEmbeddingTaggingPolicyService>();
        services.AddSingleton<ICodeMentorPolicyService, FileCodeMentorPolicyService>();
        services.AddSingleton<IRubricCatalogService, FileRubricCatalogService>();
        services.AddSingleton<IGroundTruthCatalogService, FileGroundTruthCatalogService>();
        services.AddSingleton<IContextPackStore, FileContextPackStore>();
        services.AddSingleton<IDocumentParser, DemoDocumentParser>();
        services.AddSingleton<IChunkingService, DemoChunkingService>();
        services.AddSingleton<IQuestionSchemaService, QuestionSchemaService>();
        services.AddSingleton<IQuestionPersistenceMapper, QuestionPersistenceMapper>();
        services.AddSingleton<IDifficultyAlignmentService, DifficultyAlignmentService>();
        services.AddSingleton<IRetrievalPlannerService, RetrievalPlannerService>();
        services.AddScoped<IAiPipelineOrchestrator, AiPipelineOrchestrator>();

        return services;
    }

    private static Uri BuildBaseUri(string rawBaseUrl)
    {
        var normalized = string.IsNullOrWhiteSpace(rawBaseUrl) ? "http://localhost/" : rawBaseUrl.Trim();
        if (!normalized.EndsWith("/", StringComparison.Ordinal))
        {
            normalized += "/";
        }

        return new Uri(normalized, UriKind.Absolute);
    }
}
