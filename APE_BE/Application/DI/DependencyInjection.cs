using Application.Interfaces;
using Application.Options;
using Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<GradingWorkerOptions>()
            .Bind(configuration.GetSection(
                GradingWorkerOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<PECodeRunOptions>();

        services.AddSingleton<
            IValidateOptions<GradingWorkerOptions>,
            GradingWorkerOptionsValidator>();

        services.AddSingleton(TimeProvider.System);

        services.AddScoped<
            ISubmissionScoringService,
            EqualWeightSubmissionScoringService>();

        services.AddScoped<
            ISubmissionResultEvaluator,
            SubmissionResultEvaluator>();

        services.AddScoped<
            IExecutionFeedbackDiagnosticParser,
            ExecutionFeedbackDiagnosticParser>();

        services.AddScoped<
            IPECodeRunService,
            PECodeRunService>();

        services.AddScoped<
            ISubmissionGradingProcessor,
            SubmissionGradingProcessor>();

        return services;
    }
}
