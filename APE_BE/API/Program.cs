using System.Text;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using API.Configuration;
using API.Middlewares;
using Application;
using Application.Interfaces;
using Application.Options;
using Application.Services;
using Infrastructure.AI;
using Infrastructure.Auth;
using Infrastructure.Data;
using Infrastructure.Judge0;
using Infrastructure.Persistence;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddProjectDotEnv(
    builder.Environment.ContentRootPath);

#region Logging

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Logging.Configure(options =>
{
    options.ActivityTrackingOptions =
        ActivityTrackingOptions.TraceId |
        ActivityTrackingOptions.SpanId |
        ActivityTrackingOptions.ParentId;
});

#endregion

#region Framework services

builder.Services.AddMemoryCache();

builder.Services
    .AddDataProtection()
    .SetApplicationName("APE_BE");

builder.Services.AddHttpContextAccessor();
builder.Services.AddProblemDetails();
builder.Services.AddResponseCompression();
builder.Services.AddResponseCaching();
builder.Services.AddHealthChecks();

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();

#endregion

#region Database

builder.Services.AddSingleton<DbContext>(_ =>
{
    var connectionString =
        builder.Configuration.GetConnectionString("MongoDb")
        ?? throw new InvalidOperationException(
            "ConnectionStrings:MongoDb is not configured.");

    var databaseName =
        builder.Configuration["DatabaseName"]
        ?? throw new InvalidOperationException(
            "DatabaseName is not configured.");

    return new DbContext(
        connectionString,
        databaseName);
});

#endregion

#region Options and module registration

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection(
        JwtSettings.SectionName));

builder.Services.Configure<GoogleAuthConfig>(
    builder.Configuration.GetSection(
        GoogleAuthConfig.SectionName));

builder.Services.Configure<AIOptions>(
    builder.Configuration.GetSection("AI"));

builder.Services.Configure<AzureBlobStorageOptions>(
    builder.Configuration.GetSection(
        AzureBlobStorageOptions.SectionName));

builder.Services.Configure<PayOSOptions>(
    builder.Configuration.GetSection(
        PayOSOptions.SectionName));

builder.Services.AddApplication(
    builder.Configuration);

builder.Services.AddJudge0(
    builder.Configuration);

#endregion

#region CORS

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AllowFrontend",
        policy =>
        {
            policy
                .WithOrigins(
                    "http://localhost:3000",
                    "http://127.0.0.1:3000")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
});

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPracticeSessionRepository, PracticeSessionRepository>();
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<ITopupPackageRepository, TopupPackageRepository>();
builder.Services.AddScoped<IAIExtractionDraftRepository, AIExtractionDraftRepository>();
builder.Services.AddScoped<IAIContextPackRepository, AIContextPackRepository>();
builder.Services.AddScoped<IKnowledgeRetrievalRepository, KnowledgeRetrievalRepository>();

builder.Services.Configure<AIOptions>(builder.Configuration.GetSection("AI"));
builder.Services.Configure<AzureBlobStorageOptions>(builder.Configuration.GetSection(AzureBlobStorageOptions.SectionName));
builder.Services.Configure<PayOSOptions>(builder.Configuration.GetSection(PayOSOptions.SectionName));
builder.Services.AddSingleton<IAISecretProtector, AISecretProtector>();
builder.Services.AddScoped<IAIProviderSettingsResolver, DbFirstAIProviderSettingsResolver>();
builder.Services.AddScoped<IAIProviderCredentialAdminService, AIProviderCredentialAdminService>();
builder.Services.AddScoped<IAIAgentRoutingConfigService, DbFirstAIAgentRoutingConfigService>();
builder.Services.AddScoped<IAIAgentAdminService, AIAgentAdminService>();
builder.Services.AddScoped<IAIClientFactory, AIClientFactory>();
builder.Services.AddScoped<IAIExecutionService, AIExecutionService>();
builder.Services.AddScoped<IAIFeatureRoutingService, AIFeatureRoutingService>();
builder.Services.AddScoped<IAIPromptService, AIPromptService>();
builder.Services.AddScoped<IAIArtifactCatalogService, DbFirstAIArtifactCatalogService>();
builder.Services.AddSingleton<IAITagTaxonomyProvider, DbFirstAITagTaxonomyProvider>();
builder.Services.AddScoped<IAIUsageLogRepository, AIUsageLogRepository>();
builder.Services.AddScoped<IAIVndBillingTransactionRepository, AIVndBillingTransactionRepository>();
builder.Services.AddScoped<IAIMentorFeedbackRepository, AIMentorFeedbackRepository>();
builder.Services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();
builder.Services.AddScoped<IAIVndBillingService, AIVndBillingService>();
builder.Services.AddHttpClient<OpenAITextClient>(client => client.Timeout = TimeSpan.FromSeconds(120));
builder.Services.AddHttpClient<DeepSeekTextClient>(client => client.Timeout = TimeSpan.FromSeconds(120));
builder.Services.AddHttpClient<GeminiTextClient>(client => client.Timeout = TimeSpan.FromSeconds(120));
builder.Services.AddHttpClient<CohereTextClient>(client => client.Timeout = TimeSpan.FromSeconds(120));
builder.Services.AddHttpClient<CohereEmbeddingClient>(client => client.Timeout = TimeSpan.FromSeconds(120));
builder.Services.AddHttpClient<IAIProviderModelCatalogService, AIProviderModelCatalogService>(client => client.Timeout = TimeSpan.FromSeconds(120));
builder.Services.AddScoped<IAITextClient>(sp => sp.GetRequiredService<OpenAITextClient>());
builder.Services.AddScoped<IAITextClient>(sp => sp.GetRequiredService<DeepSeekTextClient>());
builder.Services.AddScoped<IAITextClient>(sp => sp.GetRequiredService<GeminiTextClient>());
builder.Services.AddScoped<IAITextClient>(sp => sp.GetRequiredService<CohereTextClient>());
builder.Services.AddScoped<IAIEmbeddingClient>(sp => sp.GetRequiredService<CohereEmbeddingClient>());
builder.Services.AddScoped<IAITextClient, DisabledAITextClient>();
builder.Services.AddScoped<IAIEmbeddingClient, DisabledAIEmbeddingClient>();
builder.Services.AddScoped<IFileExtractionService, FileExtractionService>();
builder.Services.AddScoped<IAIExtractedContentService, AIExtractedContentService>();
builder.Services.AddScoped<IAIGatekeeperService, AIGatekeeperService>();
builder.Services.AddScoped<IAIEmbeddingTaggingService, AIEmbeddingTaggingService>();
builder.Services.AddScoped<IRetrievalPlannerService, RetrievalPlannerService>();
builder.Services.AddScoped<IQuestionGenerationReviewService, QuestionGenerationReviewService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<IKnowledgeChunkRepository, KnowledgeChunkRepository>();

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.Configure<GoogleAuthConfig>(builder.Configuration.GetSection("GoogleAuth"));
builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();
builder.Services.AddSingleton<IGoogleAuthService, GoogleAuthService>();

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<IPESubmissionRepository, PESubmissionRepository>();
builder.Services.AddScoped<IFESubmissionRepository, FESubmissionRepository>();
builder.Services.AddScoped<IPEQuestionRepository, PEQuestionRepository>();
builder.Services.AddScoped<IFEQuestionRepository, FEQuestionRepository>();
builder.Services.AddScoped<ExamService>();
builder.Services.AddScoped<PESubmissionService>();
builder.Services.AddScoped<FESubmissionService>();
builder.Services.AddScoped<PracticeService>();
builder.Services.AddScoped<GamificationService>();
builder.Services.AddScoped<QuestionImportService>();
builder.Services.AddScoped<CodeMentorService>();
builder.Services.AddScoped<IExamRepository, ExamRepository>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IPayOSService, PayOSService>();
builder.Services.AddScoped<WalletTopupService>();

builder.Services.AddHostedService<API.Workers.Judge0Worker>();
builder.Services.AddHostedService<API.Workers.PaymentStatusWorker>();

#endregion

#region Authentication and authorization

var jwtSettings =
    builder.Configuration
        .GetRequiredSection(JwtSettings.SectionName)
        .Get<JwtSettings>()
    ?? throw new InvalidOperationException(
        "JwtSettings configuration is invalid.");

if (string.IsNullOrWhiteSpace(jwtSettings.Secret))
{
    throw new InvalidOperationException(
        "JwtSettings:Secret is required.");
}

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata =
            !builder.Environment.IsDevelopment();

        options.SaveToken = false;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            jwtSettings.Secret)),

                ClockSkew = TimeSpan.FromSeconds(30)
            };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger =
                    context.HttpContext.RequestServices
                        .GetRequiredService<
                            ILogger<Program>>();

                logger.LogWarning(
                    context.Exception,
                    "JWT authentication failed.");

                return Task.CompletedTask;
            },

            OnChallenge = context =>
            {
                context.HandleResponse();

                context.Response.StatusCode =
                    StatusCodes.Status401Unauthorized;

                context.Response.ContentType =
                    "application/json";

                return context.Response.WriteAsJsonAsync(
                    new
                    {
                        success = false,
                        error = "Unauthorized."
                    });
            },

            OnForbidden = context =>
            {
                context.Response.StatusCode =
                    StatusCodes.Status403Forbidden;

                context.Response.ContentType =
                    "application/json";

                return context.Response.WriteAsJsonAsync(
                    new
                    {
                        success = false,
                        error = "Forbidden."
                    });
            }
        };
    });

builder.Services.AddAuthorization();

#endregion

#region Repositories

builder.Services.AddScoped<
    IUserRepository,
    UserRepository>();

builder.Services.AddScoped<
    IPracticeSessionRepository,
    PracticeSessionRepository>();

builder.Services.AddScoped<
    ICourseRepository,
    CourseRepository>();

builder.Services.AddScoped<
    IExamRepository,
    ExamRepository>();

builder.Services.AddScoped<
    IDocumentRepository,
    DocumentRepository>();

builder.Services.AddScoped<
    ITopupPackageRepository,
    TopupPackageRepository>();

builder.Services.AddScoped<
    IKnowledgeChunkRepository,
    KnowledgeChunkRepository>();

builder.Services.AddScoped<
    IAIExtractionDraftRepository,
    AIExtractionDraftRepository>();

builder.Services.AddScoped<
    IAIContextPackRepository,
    AIContextPackRepository>();

builder.Services.AddScoped<
    IKnowledgeRetrievalRepository,
    KnowledgeRetrievalRepository>();

builder.Services.AddScoped<
    IFESubmissionRepository,
    FESubmissionRepository>();

builder.Services.AddScoped<
    IPESubmissionRepository,
    PESubmissionRepository>();

builder.Services.AddScoped<
    IFEQuestionRepository,
    FEQuestionRepository>();

builder.Services.AddScoped<
    IPEQuestionRepository,
    PEQuestionRepository>();

builder.Services.AddScoped<
    IAIUsageLogRepository,
    AIUsageLogRepository>();

builder.Services.AddScoped<
    IAIMentorFeedbackRepository,
    AIMentorFeedbackRepository>();

builder.Services.AddScoped<
    IPaymentRepository,
    PaymentRepository>();

builder.Services.AddScoped<
    ISystemSettingRepository,
    SystemSettingRepository>();

builder.Services.AddScoped<
    IIdGenerator,
    MongoObjectIdGenerator>();

#endregion

#region Authentication services

builder.Services.AddSingleton<
    IJwtTokenGenerator,
    JwtTokenGenerator>();

builder.Services.AddSingleton<
    IGoogleTokenValidator,
    GoogleTokenValidator>();

builder.Services.AddSingleton<
    IGoogleAuthService,
    GoogleAuthService>();

builder.Services.AddScoped<
    IAuthService,
    AuthService>();

#endregion

#region AI services

builder.Services.AddSingleton<
    IAISecretProtector,
    AISecretProtector>();

builder.Services.AddScoped<
    IAIProviderSettingsResolver,
    DbFirstAIProviderSettingsResolver>();

builder.Services.AddScoped<
    IAIProviderCredentialAdminService,
    AIProviderCredentialAdminService>();

builder.Services.AddScoped<
    IAIAgentRoutingConfigService,
    DbFirstAIAgentRoutingConfigService>();

builder.Services.AddScoped<
    IAIAgentAdminService,
    AIAgentAdminService>();

builder.Services.AddScoped<
    IAIClientFactory,
    AIClientFactory>();

builder.Services.AddScoped<
    IAIExecutionService,
    AIExecutionService>();

builder.Services.AddScoped<
    IAIFeatureRoutingService,
    AIFeatureRoutingService>();

builder.Services.AddScoped<
    IAIPromptService,
    AIPromptService>();

builder.Services.AddScoped<
    IAIArtifactCatalogService,
    DbFirstAIArtifactCatalogService>();

builder.Services.AddHttpClient<OpenAITextClient>(
    client =>
        client.Timeout = TimeSpan.FromSeconds(120));

builder.Services.AddHttpClient<DeepSeekTextClient>(
    client =>
        client.Timeout = TimeSpan.FromSeconds(120));

builder.Services.AddHttpClient<GeminiTextClient>(
    client =>
        client.Timeout = TimeSpan.FromSeconds(120));

builder.Services.AddHttpClient<CohereTextClient>(
    client =>
        client.Timeout = TimeSpan.FromSeconds(120));

builder.Services.AddHttpClient<CohereEmbeddingClient>(
    client =>
        client.Timeout = TimeSpan.FromSeconds(120));

builder.Services.AddHttpClient<
    IAIProviderModelCatalogService,
    AIProviderModelCatalogService>(
        client =>
            client.Timeout = TimeSpan.FromSeconds(120));

builder.Services.AddScoped<IAITextClient>(
    serviceProvider =>
        serviceProvider.GetRequiredService<
            OpenAITextClient>());

builder.Services.AddScoped<IAITextClient>(
    serviceProvider =>
        serviceProvider.GetRequiredService<
            DeepSeekTextClient>());

builder.Services.AddScoped<IAITextClient>(
    serviceProvider =>
        serviceProvider.GetRequiredService<
            GeminiTextClient>());

builder.Services.AddScoped<IAITextClient>(
    serviceProvider =>
        serviceProvider.GetRequiredService<
            CohereTextClient>());

builder.Services.AddScoped<IAIEmbeddingClient>(
    serviceProvider =>
        serviceProvider.GetRequiredService<
            CohereEmbeddingClient>());

builder.Services.AddScoped<
    IAITextClient,
    DisabledAITextClient>();

builder.Services.AddScoped<
    IAIEmbeddingClient,
    DisabledAIEmbeddingClient>();

builder.Services.AddScoped<
    IAIExtractedContentService,
    AIExtractedContentService>();

builder.Services.AddScoped<
    IAIGatekeeperService,
    AIGatekeeperService>();

builder.Services.AddScoped<
    IAIEmbeddingTaggingService,
    AIEmbeddingTaggingService>();

builder.Services.AddScoped<
    IRetrievalPlannerService,
    RetrievalPlannerService>();

builder.Services.AddScoped<
    IQuestionGenerationReviewService,
    QuestionGenerationReviewService>();

#endregion

#region Application and infrastructure services

builder.Services.AddScoped<
    ICourseService,
    CourseService>();

builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ExamService>();
builder.Services.AddScoped<PracticeService>();
builder.Services.AddScoped<FESubmissionService>();
builder.Services.AddScoped<QuestionImportService>();
builder.Services.AddScoped<GamificationService>();
builder.Services.AddScoped<CodeMentorService>();

builder.Services.AddScoped<
    IFileExtractionService,
    FileExtractionService>();

builder.Services.AddScoped<
    IFileStorageService,
    FileStorageService>();

builder.Services.AddScoped<
    IPayOSService,
    PayOSService>();

builder.Services.AddScoped<WalletTopupService>();

builder.Services.AddScoped<PESubmissionService>();

builder.Services.AddScoped<
    IPESubmissionService>(
        serviceProvider =>
            serviceProvider.GetRequiredService<
                PESubmissionService>());

builder.Services.AddSingleton(
    Channel.CreateUnbounded<
        DocumentProcessingJob>());

#endregion

#region Background workers

builder.Services.AddHostedService<
    API.Workers.Judge0Worker>();

builder.Services.AddHostedService<
    API.Workers.PaymentStatusWorker>();

#endregion

#region Rate limiting

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter(
        "auth",
        configure =>
        {
            configure.PermitLimit = 10;
            configure.Window =
                TimeSpan.FromMinutes(1);
            configure.QueueLimit = 0;
            configure.QueueProcessingOrder =
                QueueProcessingOrder.OldestFirst;
        });

    options.OnRejected =
        async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode =
                StatusCodes.Status429TooManyRequests;

            await context.HttpContext.Response
                .WriteAsJsonAsync(
                    new
                    {
                        success = false,
                        error = "Too many requests."
                    },
                    cancellationToken);
        };
});

#endregion

#region Swagger

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "APECore API",
            Version = "v1",
            Description =
                "AI-powered Examination & Programming Practice Platform"
        });

    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme =
                JwtBearerDefaults.AuthenticationScheme,
            BearerFormat = "JWT",
            Description = "Bearer {access_token}"
        });

    options.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference =
                        new OpenApiReference
                        {
                            Id = "Bearer",
                            Type =
                                ReferenceType.SecurityScheme
                        }
                },
                Array.Empty<string>()
            }
        });

    var xmlFile =
        $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";

    var xmlPath =
        Path.Combine(
            AppContext.BaseDirectory,
            xmlFile);

    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

#endregion

var app = builder.Build();

app.Services.ValidateJudge0OptionsOnStartup();

#region Database and AI initialization

using (var scope = app.Services.CreateScope())
{
    var logger =
        scope.ServiceProvider
            .GetRequiredService<ILogger<Program>>();

    try
    {
        var database =
            scope.ServiceProvider
                .GetRequiredService<DbContext>();

        await DbInitializer.InitializeAsync(
            database,
            app.Lifetime.ApplicationStopping);

        var runStartupSeed =
            app.Configuration.GetValue<bool>("Database:RunSeedOnStartup");

        if (runStartupSeed)
        {
            var secretProtector =
                scope.ServiceProvider
                    .GetRequiredService<IAISecretProtector>();

            await AIArtifactDbSeeder.SeedAsync(
                database,
                app.Configuration,
                app.Environment);

            await AIAgentBootstrapSeeder.SeedAsync(
                database,
                app.Configuration,
                secretProtector);

            logger.LogInformation(
                "Database indexes initialized and startup seed completed.");
        }
        else
        {
            logger.LogInformation(
                "Database indexes initialized. Startup seed is disabled.");
        }
    }
    catch (Exception exception)
    {
        logger.LogCritical(
            exception,
            "Database or AI initialization failed.");

        throw;
    }
}

#endregion

#region Middleware pipeline

app.UseMiddleware<ExceptionMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseResponseCompression();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowFrontend");
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/swagger/v1/swagger.json",
            "APECore API v1");

        options.DisplayRequestDuration();
        options.EnableTryItOutByDefault();
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.UseResponseCaching();

app.MapControllers();
app.MapHealthChecks("/health");

#endregion

app.Run();

public partial class Program
{
}
