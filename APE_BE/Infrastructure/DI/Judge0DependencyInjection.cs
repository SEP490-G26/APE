using System.Net.Http.Headers;
using Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure.Judge0;

public static class Judge0DependencyInjection
{
    public static IServiceCollection AddJudge0(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(configuration);

        services
            .AddOptions<Judge0Options>()
            .Bind(configuration.GetSection(
                Judge0Options.SectionName))
            .ValidateOnStart();

        services.AddSingleton<
            IValidateOptions<Judge0Options>,
            Judge0OptionsValidator>();

        services.AddSingleton<
            IJudge0MultiFileProfileResolver,
            Judge0MultiFileProfileResolver>();

        services.AddSingleton<
            IJudge0SourcePackageBuilder,
            Judge0SourcePackageBuilder>();

        services.AddHttpClient<
                ICodeExecutionClient,
                Judge0Client>((serviceProvider, client) =>
                {
                    var options = serviceProvider
                        .GetRequiredService<IOptions<Judge0Options>>()
                        .Value;

                    var normalizedBaseUrl =
                        NormalizeKnownJudge0RapidApiBaseUrl(
                            options.BaseUrl);

                    client.BaseAddress = new Uri(
                        normalizedBaseUrl.TrimEnd('/') + "/");

                    client.Timeout = TimeSpan.FromSeconds(
                        options.TimeoutSeconds);

                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue(
                            "application/json"));

                    switch (options.GetAuthenticationModeOrThrow())
                    {
                        case Judge0AuthenticationMode.RapidApi:
                            client.DefaultRequestHeaders.TryAddWithoutValidation(
                                "X-RapidAPI-Key",
                                options.RapidApiKey!.Trim());
                            client.DefaultRequestHeaders.TryAddWithoutValidation(
                                "X-RapidAPI-Host",
                                NormalizeKnownJudge0RapidApiHost(
                                    NormalizeRapidApiHostHeaderValue(
                                        options.RapidApiHost!)));
                            break;

                        case Judge0AuthenticationMode.Judge0AuthToken:
                            client.DefaultRequestHeaders.TryAddWithoutValidation(
                                "X-Auth-Token",
                                options.AuthToken!.Trim());
                            break;

                        case Judge0AuthenticationMode.None:
                            break;
                    }
                })
            .RemoveAllLoggers();

        return services;
    }

    public static void ValidateJudge0OptionsOnStartup(
        this IServiceProvider services)
    {
        _ = services
            .GetRequiredService<IOptions<Judge0Options>>()
            .Value;
    }

    private static string NormalizeRapidApiHostHeaderValue(
        string host)
    {
        return host
            .Trim()
            .TrimEnd('.');
    }

    private static string NormalizeKnownJudge0RapidApiBaseUrl(
        string baseUrl)
    {
        var trimmed = baseUrl.Trim();

        return trimmed.Replace(
            "https://judge-ce.p.rapidapi.com/",
            "https://judge0-ce.p.rapidapi.com/",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeKnownJudge0RapidApiHost(
        string host)
    {
        return string.Equals(
            host,
            "judge-ce.p.rapidapi.com",
            StringComparison.OrdinalIgnoreCase)
            ? "judge0-ce.p.rapidapi.com"
            : host;
    }
}
