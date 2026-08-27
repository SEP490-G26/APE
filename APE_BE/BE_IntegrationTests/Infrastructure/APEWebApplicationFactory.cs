using System.Net.Http.Headers;
using API.Workers;
using Application.Interfaces;
using BE_IntegrationTests.External;
using Application.Services;
using Infrastructure.Auth;
using Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace BE_IntegrationTests.Infrastructure;

public sealed class APEWebApplicationFactory
    : WebApplicationFactory<Program>
{
    private readonly string _mongoConnectionString;
    private readonly string _databaseName;

    public APEWebApplicationFactory(
        string mongoConnectionString,
        string databaseName)
    {
        _mongoConnectionString = mongoConnectionString;
        _databaseName = databaseName;
        ExecutionClient = new ControlledCodeExecutionClient();
        GoogleTokenValidator = new ControlledGoogleTokenValidator();
    }

    public ControlledCodeExecutionClient ExecutionClient { get; }

    public ControlledGoogleTokenValidator GoogleTokenValidator { get; }

    public ControlledPayOSService PayOS { get; } = new();

    public ControlledFileStorageService FileStorage { get; } = new();

    public ControlledFileExtractionService FileExtraction { get; } = new();

    public ControlledAIExtractedContentService ExtractedContent { get; } = new();

    public ControlledAIGatekeeperService Gatekeeper { get; } = new();

    public ControlledAIEmbeddingTaggingService EmbeddingTagging { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["ConnectionStrings:MongoDb"] = _mongoConnectionString,
                ["DatabaseName"] = _databaseName,
                ["Database:RunSeedOnStartup"] = "false",
                ["SeedData:Enabled"] = "false",
                ["GradingWorker:IdleDelaySeconds"] = "1",
                ["GradingWorker:BatchSize"] = "4",
                ["GradingWorker:InitialRetryDelaySeconds"] = "1",
                ["GradingWorker:LeaseSeconds"] = "20",
                ["GradingWorker:LeaseRenewalSeconds"] = "5",
                ["GradingWorker:PollingIntervalSeconds"] = "1",
                ["GradingWorker:MaximumPollingAttempts"] = "5",
                ["GradingWorker:MaximumProcessingSeconds"] = "20"
            };

            configBuilder.AddInMemoryCollection(overrides);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContext>();
            services.AddSingleton(_ => new DbContext(_mongoConnectionString, _databaseName));

            services.RemoveAll<ICodeExecutionClient>();
            services.AddSingleton(ExecutionClient);
            services.AddSingleton<ICodeExecutionClient>(sp => sp.GetRequiredService<ControlledCodeExecutionClient>());

            services.RemoveAll<IAIFeatureRoutingService>();
            services.AddSingleton<IAIFeatureRoutingService, ControlledMentorFeatureRoutingService>();

            services.RemoveAll<IAIArtifactCatalogService>();
            services.AddSingleton<IAIArtifactCatalogService, ControlledMentorArtifactCatalogService>();

            services.RemoveAll<IAIExecutionService>();
            services.AddSingleton<IAIExecutionService, ControlledMentorAiExecutionService>();

            services.RemoveAll<IAIPromptService>();
            services.AddSingleton<IAIPromptService, AIPromptService>();

            services.RemoveAll<IAIVndBillingService>();
            services.AddSingleton<IAIVndBillingService, ControlledMentorBillingService>();

            services.RemoveAll<IAICreditPolicyService>();
            services.AddSingleton<IAICreditPolicyService, ControlledAICreditPolicyService>();

            services.RemoveAll<IAIProviderModelCatalogService>();
            services.AddSingleton<IAIProviderModelCatalogService, ControlledAIProviderModelCatalogService>();

            services.RemoveAll<IGoogleTokenValidator>();
            services.AddSingleton(GoogleTokenValidator);
            services.AddSingleton<IGoogleTokenValidator>(sp => sp.GetRequiredService<ControlledGoogleTokenValidator>());

            services.RemoveAll<IFileStorageService>();
            services.AddSingleton(FileStorage);
            services.AddSingleton<IFileStorageService>(sp => sp.GetRequiredService<ControlledFileStorageService>());

            services.RemoveAll<IFileExtractionService>();
            services.AddSingleton(FileExtraction);
            services.AddSingleton<IFileExtractionService>(sp => sp.GetRequiredService<ControlledFileExtractionService>());

            services.RemoveAll<IAIExtractedContentService>();
            services.AddSingleton(ExtractedContent);
            services.AddSingleton<IAIExtractedContentService>(sp => sp.GetRequiredService<ControlledAIExtractedContentService>());

            services.RemoveAll<IAIGatekeeperService>();
            services.AddSingleton(Gatekeeper);
            services.AddSingleton<IAIGatekeeperService>(sp => sp.GetRequiredService<ControlledAIGatekeeperService>());

            services.RemoveAll<IAIEmbeddingTaggingService>();
            services.AddSingleton(EmbeddingTagging);
            services.AddSingleton<IAIEmbeddingTaggingService>(sp => sp.GetRequiredService<ControlledAIEmbeddingTaggingService>());

            services.RemoveAll<IPayOSService>();
            services.AddSingleton(PayOS);
            services.AddSingleton<IPayOSService>(sp => sp.GetRequiredService<ControlledPayOSService>());

            RemoveHostedService<PaymentStatusWorker>(services);

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "IntegrationOrBearer";
                    options.DefaultChallengeScheme = "IntegrationOrBearer";
                    options.DefaultScheme = "IntegrationOrBearer";
                })
                .AddPolicyScheme(
                    "IntegrationOrBearer",
                    "Integration test or JWT bearer",
                    options =>
                    {
                        options.ForwardDefaultSelector = context =>
                            context.Request.Headers.ContainsKey(IntegrationTestAuthHandler.UserIdHeader)
                                ? IntegrationTestAuthHandler.SchemeName
                                : JwtBearerDefaults.AuthenticationScheme;
                    })
                .AddScheme<AuthenticationSchemeOptions, IntegrationTestAuthHandler>(
                    IntegrationTestAuthHandler.SchemeName,
                    _ => { });
        });
    }

    public HttpClient CreateAuthenticatedClient(TestIdentity identity)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Add(IntegrationTestAuthHandler.UserIdHeader, identity.UserId);
        client.DefaultRequestHeaders.Add(IntegrationTestAuthHandler.RoleHeader, identity.Role);
        client.DefaultRequestHeaders.Add(IntegrationTestAuthHandler.EmailHeader, identity.Email);
        client.DefaultRequestHeaders.Add(IntegrationTestAuthHandler.NameHeader, identity.FullName);

        return client;
    }

    private static void RemoveHostedService<THostedService>(IServiceCollection services)
        where THostedService : class, IHostedService
    {
        var descriptors = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(THostedService))
            .ToList();

        foreach (var descriptor in descriptors)
        {
            services.Remove(descriptor);
        }
    }
}
