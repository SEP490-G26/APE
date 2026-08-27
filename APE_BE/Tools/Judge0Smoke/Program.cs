using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using Application.Exceptions;
using Application.Interfaces;
using Application.Models;
using Domain.Entities;
using Infrastructure.Judge0;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var exitCode = await ProgramMainAsync(args);
return exitCode;

static async Task<int> ProgramMainAsync(
    string[] args)
{
    var command = args.FirstOrDefault()?.Trim().ToLowerInvariant();
    if (command is not ("health" or "success" or "success-diagnose" or "compile-error"))
    {
        Console.WriteLine(
            "Usage: dotnet run --project .\\Tools\\Judge0Smoke\\Judge0Smoke.csproj -- <health|success|success-diagnose|compile-error>");
        return 2;
    }

    var environmentName =
        Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
        Environments.Development;

    var apiProjectPath = FindApiProjectPath();
    var apiDirectory = Path.GetDirectoryName(apiProjectPath)
        ?? throw new InvalidOperationException(
            "Unable to resolve API directory.");

    var configuration = BuildConfiguration(
        apiDirectory,
        environmentName);

    var readiness = EvaluateReadiness(configuration);
    var consoleRecorder = new ConsoleRecorder();
    consoleRecorder.WriteLine(
        $"AuthenticationMode: {readiness.AuthenticationMode}");
    consoleRecorder.WriteLine(
        $"BaseUrl: {readiness.BaseUrl}");
    consoleRecorder.WriteLine(
        $"RapidApiHost: {readiness.RapidApiHost}");
    consoleRecorder.WriteLine(
        $"RapidApiKey configured: {readiness.RapidApiKeyConfigured.ToString().ToLowerInvariant()}");

    using var serviceProvider = BuildServiceProvider(
        configuration,
        environmentName);

    try
    {
        serviceProvider.ValidateJudge0OptionsOnStartup();
        consoleRecorder.WriteLine("StartupValidation: passed");
    }
    catch (OptionsValidationException exception)
    {
        consoleRecorder.WriteLine("StartupValidation: failed");
        consoleRecorder.WriteLine(
            $"ValidationCategory: {ClassifyValidationFailure(exception)}");
        return 1;
    }

    if (!readiness.CanRunLiveRequests)
    {
        consoleRecorder.WriteLine("LiveExecution: skipped");
        return 1;
    }

    var client = serviceProvider.GetRequiredService<ICodeExecutionClient>();
    var loggerProvider = serviceProvider.GetRequiredService<InMemoryLoggerProvider>();
    var httpCounter = serviceProvider.GetRequiredService<HttpRequestCounter>();
    var harness = new SmokeHarness(
        client,
        loggerProvider,
        httpCounter,
        consoleRecorder,
        configuration);

    return command switch
    {
        "health" => await harness.RunHealthAsync(),
        "success" => await harness.RunSuccessAsync(detailedDiagnostics: false),
        "success-diagnose" => await harness.RunSuccessAsync(detailedDiagnostics: true),
        "compile-error" => await harness.RunCompileErrorAsync(),
        _ => 2
    };
}

static IConfiguration BuildConfiguration(
    string apiDirectory,
    string environmentName)
{
    return new ConfigurationBuilder()
        .SetBasePath(apiDirectory)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        .AddJsonFile(
            $"appsettings.{environmentName}.json",
            optional: true,
            reloadOnChange: false)
        .AddUserSecrets<HarnessHostEnvironment>(optional: true, reloadOnChange: false)
        .AddEnvironmentVariables()
        .Build();
}

static ReadinessResult EvaluateReadiness(
    IConfiguration configuration)
{
    var authenticationMode = configuration["Judge0:AuthenticationMode"];
    var baseUrl = configuration["Judge0:BaseUrl"];
    var rapidApiHost = configuration["Judge0:RapidApiHost"];
    var rapidApiKey = configuration["Judge0:RapidApiKey"];

    var authenticationModeStatus =
        string.Equals(
            authenticationMode?.Trim(),
            nameof(Judge0AuthenticationMode.RapidApi),
            StringComparison.OrdinalIgnoreCase)
            ? "valid"
            : string.IsNullOrWhiteSpace(authenticationMode)
                ? "missing"
                : "invalid";

    var baseUrlStatus =
        Uri.TryCreate(
            baseUrl?.Trim(),
            UriKind.Absolute,
            out var parsedBaseUri) &&
        parsedBaseUri.Scheme is "http" or "https"
            ? "valid"
            : string.IsNullOrWhiteSpace(baseUrl)
                ? "missing"
                : "invalid";

    var rapidApiHostStatus =
        TryNormalizeHost(rapidApiHost, out var normalizedRapidApiHost) &&
        parsedBaseUri is not null &&
        string.Equals(
            normalizedRapidApiHost,
            NormalizeHost(parsedBaseUri.Host),
            StringComparison.OrdinalIgnoreCase)
            ? "valid"
            : string.IsNullOrWhiteSpace(rapidApiHost)
                ? "missing"
                : "invalid";

    var rapidApiKeyConfigured =
        !string.IsNullOrWhiteSpace(rapidApiKey);

    return new ReadinessResult(
        authenticationModeStatus,
        baseUrlStatus,
        rapidApiHostStatus,
        rapidApiKeyConfigured,
        CanRunLiveRequests:
            authenticationModeStatus == "valid" &&
            baseUrlStatus == "valid" &&
            rapidApiHostStatus == "valid" &&
            rapidApiKeyConfigured);
}

static ServiceProvider BuildServiceProvider(
    IConfiguration configuration,
    string environmentName)
{
    var services = new ServiceCollection();
    var loggerProvider = new InMemoryLoggerProvider();

    services.AddSingleton<IConfiguration>(configuration);
    services.AddSingleton<IHostEnvironment>(
        new HarnessHostEnvironment(environmentName));
    services.AddSingleton(loggerProvider);
    services.AddSingleton<HttpRequestCounter>();
    services.AddSingleton<IHttpMessageHandlerBuilderFilter, CountingHttpMessageHandlerBuilderFilter>();
    services.AddLogging(builder =>
    {
        builder.ClearProviders();
        builder.AddProvider(loggerProvider);
    });

    services.AddJudge0(configuration);
    return services.BuildServiceProvider();
}

static string FindApiProjectPath()
{
    var directory = new DirectoryInfo(
        Directory.GetCurrentDirectory());

    while (directory is not null)
    {
        var rootSolutionPath = Path.Combine(
            directory.FullName,
            "APE_Core.sln");
        var rootApiProjectPath = Path.Combine(
            directory.FullName,
            "API",
            "API.csproj");
        var nestedSolutionPath = Path.Combine(
            directory.FullName,
            "APE_BE",
            "APE_Core.sln");
        var nestedApiProjectPath = Path.Combine(
            directory.FullName,
            "APE_BE",
            "API",
            "API.csproj");

        if (File.Exists(rootSolutionPath) &&
            File.Exists(rootApiProjectPath))
        {
            return rootApiProjectPath;
        }

        if (File.Exists(nestedSolutionPath) &&
            File.Exists(nestedApiProjectPath))
        {
            return nestedApiProjectPath;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException(
        "Unable to locate APE_BE/API/API.csproj from the current working directory.");
}

static bool TryNormalizeHost(
    string? value,
    out string normalizedHost)
{
    normalizedHost = string.Empty;

    if (string.IsNullOrWhiteSpace(value))
    {
        return false;
    }

    var trimmed = value.Trim();
    if (trimmed.Length == 0 ||
        trimmed.Any(char.IsWhiteSpace) ||
        trimmed.Any(char.IsControl) ||
        trimmed.Contains("://", StringComparison.Ordinal) ||
        trimmed.Contains('/', StringComparison.Ordinal) ||
        trimmed.Contains('?', StringComparison.Ordinal) ||
        trimmed.Contains('#', StringComparison.Ordinal))
    {
        return false;
    }

    var hostType = Uri.CheckHostName(trimmed);
    if (hostType is not (
            UriHostNameType.Dns or
            UriHostNameType.IPv4 or
            UriHostNameType.IPv6))
    {
        return false;
    }

    normalizedHost = NormalizeHost(trimmed);
    return true;
}

static string NormalizeHost(
    string host)
{
    return host
        .Trim()
        .TrimEnd('.')
        .ToLowerInvariant();
}

static string ClassifyValidationFailure(
    OptionsValidationException exception)
{
    var message = exception.Message;

    if (message.Contains(
            "authentication mode",
            StringComparison.OrdinalIgnoreCase))
    {
        return "AuthenticationMode";
    }

    if (message.Contains(
            "BaseUrl",
            StringComparison.OrdinalIgnoreCase))
    {
        return "BaseUrl";
    }

    if (message.Contains(
            "RapidAPI host",
            StringComparison.OrdinalIgnoreCase))
    {
        return "RapidApiHost";
    }

    if (message.Contains(
            "RapidAPI key",
            StringComparison.OrdinalIgnoreCase))
    {
        return "RapidApiKey";
    }

    if (message.Contains(
            "TimeoutSeconds",
            StringComparison.OrdinalIgnoreCase))
    {
        return "TimeoutSeconds";
    }

    return "Unknown";
}

internal sealed record ReadinessResult(
    string AuthenticationMode,
    string BaseUrl,
    string RapidApiHost,
    bool RapidApiKeyConfigured,
    bool CanRunLiveRequests);

internal sealed class SmokeHarness
{
    private const string SuccessSource =
        """
        import java.util.Scanner;

        public class Main {
            public static void main(String[] args) {
                Scanner scanner = new Scanner(System.in);
                int value = scanner.nextInt();
                System.out.print(value);
            }
        }
        """;

    private const string SuccessSourceFileName = "Main.java";
    private const string SuccessStandardInput = "7";
    private const int SuccessLanguageId = 62;
    private const int SuccessTimeLimitMs = 1000;
    private const int SuccessMemoryLimitKb = 131072;
    private const string CompileErrorSource =
        """
        public class Main {
            public static void main(String[] args) {
                System.out.println("compile failure")
            }
        }
        """;
    private const string CompileErrorSourceFileName = "Main.java";
    private const string CompileErrorStandardInput = "";
    private const int CompileErrorLanguageId = 62;
    private const int CompileErrorTimeLimitMs = 1000;
    private const int CompileErrorMemoryLimitKb = 131072;

    private readonly ICodeExecutionClient _client;
    private readonly InMemoryLoggerProvider _loggerProvider;
    private readonly HttpRequestCounter _httpCounter;
    private readonly ConsoleRecorder _consoleRecorder;
    private readonly IConfiguration _configuration;
    private int _pollRequestCount;

    public SmokeHarness(
        ICodeExecutionClient client,
        InMemoryLoggerProvider loggerProvider,
        HttpRequestCounter httpCounter,
        ConsoleRecorder consoleRecorder,
        IConfiguration configuration)
    {
        _client = client;
        _loggerProvider = loggerProvider;
        _httpCounter = httpCounter;
        _consoleRecorder = consoleRecorder;
        _configuration = configuration;
    }

    public async Task<int> RunHealthAsync()
    {
        _loggerProvider.Clear();
        _httpCounter.Reset();

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var healthy = await _client.IsHealthyAsync(
                CancellationToken.None);
            stopwatch.Stop();

            _consoleRecorder.WriteLine(
                $"HealthResult: {(healthy ? "success" : "failure")}");
            _consoleRecorder.WriteLine(
                $"HealthElapsedMilliseconds: {stopwatch.ElapsedMilliseconds}");
            _consoleRecorder.WriteLine(
                $"LiveHttpRequests: {_httpCounter.Count}");
            _consoleRecorder.WriteLine(
                $"RetryAfter: {FormatRetryAfter(_httpCounter.LastRetryAfter)}");
            _consoleRecorder.WriteLine(
                $"CorrelationId: {FormatCorrelationId(_httpCounter.LastCorrelationId)}");
            if (!healthy)
            {
                _consoleRecorder.WriteLine(
                    $"HealthErrorCategory: {ClassifyTransportFailure(_httpCounter)}");
                _consoleRecorder.WriteLine(
                    $"HealthStatusCode: {FormatStatusCode(_httpCounter.LastStatusCode)}");
            }
            return healthy ? 0 : 1;
        }
        catch (CodeExecutionClientException exception)
        {
            stopwatch.Stop();
            _consoleRecorder.WriteLine("HealthResult: failure");
            _consoleRecorder.WriteLine(
                $"HealthElapsedMilliseconds: {stopwatch.ElapsedMilliseconds}");
            _consoleRecorder.WriteLine(
                $"HealthErrorCategory: {exception.ErrorCategory}");
            _consoleRecorder.WriteLine(
                $"LiveHttpRequests: {_httpCounter.Count}");
            _consoleRecorder.WriteLine(
                $"RetryAfter: {FormatRetryAfter(exception.RetryAfter)}");
            _consoleRecorder.WriteLine("CorrelationId: unavailable");
            return 1;
        }
    }

    public async Task<int> RunSuccessAsync(
        bool detailedDiagnostics)
    {
        _loggerProvider.Clear();
        _httpCounter.Reset();
        _pollRequestCount = 0;

        var stopwatch = Stopwatch.StartNew();
        var apiKey = _configuration["Judge0:RapidApiKey"] ?? string.Empty;
        var request = CodeExecutionBatchRequest.Create(
            SuccessLanguageId,
            [CodeFile.Create(SuccessSourceFileName, SuccessSource)],
            [new CodeExecutionCaseRequest(0, SuccessStandardInput, SuccessTimeLimitMs, SuccessMemoryLimitKb)]);

        var packagingInfo = DeterminePackagingInfo(request);
        var packagingMode = request.SourceFiles.Count == 1
            ? "single-file"
            : "multi-file";

        _consoleRecorder.WriteLine($"LanguageId: {SuccessLanguageId}");
        _consoleRecorder.WriteLine($"RequestPackagingMode: {packagingMode}");
        _consoleRecorder.WriteLine($"SourceFileName: {SuccessSourceFileName}");
        _consoleRecorder.WriteLine($"SourceFileCount: {request.SourceFiles.Count}");
        _consoleRecorder.WriteLine($"SourceCharacterCount: {SuccessSource.Length}");
        _consoleRecorder.WriteLine(
            $"ClassNameMatchesFileName: {SuccessSource.Contains("public class Main", StringComparison.Ordinal).ToString().ToLowerInvariant()}");
        _consoleRecorder.WriteLine($"TestcaseCount: {request.Cases.Count}");
        _consoleRecorder.WriteLine($"CaseIndex: {request.Cases[0].CaseIndex}");
        _consoleRecorder.WriteLine($"StdinCharacterCount: {SuccessStandardInput.Length}");
        _consoleRecorder.WriteLine("StdinShape: exactly-7");
        _consoleRecorder.WriteLine($"TimeLimitMs: {request.Cases[0].TimeLimitMs}");
        _consoleRecorder.WriteLine($"MemoryLimitKb: {request.Cases[0].MemoryLimitKb}");
        _consoleRecorder.WriteLine("CommandLineArgumentsSupplied: false");
        _consoleRecorder.WriteLine("CompilerOptionsSupplied: false");
        _consoleRecorder.WriteLine("ExpectedOutputSentToJudge0: false");
        _consoleRecorder.WriteLine("RequestBase64Encoding: true");
        _consoleRecorder.WriteLine(
            $"UsesAdditionalFiles: {packagingInfo.UsesAdditionalFiles.ToString().ToLowerInvariant()}");
        _consoleRecorder.WriteLine(
            $"LanguageRoutedThroughMultiFileProfile89: {packagingInfo.RoutedThroughMultiFileProfile89.ToString().ToLowerInvariant()}");

        string? providerToken = null;

        try
        {
            using var deadlineCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(60));

            var receipt = await _client.CreateBatchAsync(
                request,
                deadlineCts.Token);

            if (receipt.Tokens.Count != 1)
            {
                _consoleRecorder.WriteLine("ReceiptCount: " + receipt.Tokens.Count);
                return 1;
            }

            providerToken = receipt.Tokens[0].ProviderToken;

            var detailedResult = await PollDetailedTerminalResultAsync(
                providerToken,
                deadlineCts.Token);

            var mappedState = MapState(
                detailedResult.StatusId,
                detailedResult.StatusDescription,
                detailedResult.StandardError,
                detailedResult.Message);

            var outputMatched = string.Equals(
                NormalizeOutput(detailedResult.StandardOutput),
                SuccessStandardInput,
                StringComparison.Ordinal);

            stopwatch.Stop();

            _consoleRecorder.WriteLine("ReceiptCount: 1");
            _consoleRecorder.WriteLine(
                $"ReceiptCaseIndex: {receipt.Tokens[0].CaseIndex}");
            _consoleRecorder.WriteLine(
                $"ProviderTokenPresent: {(!string.IsNullOrWhiteSpace(providerToken)).ToString().ToLowerInvariant()}");
            _consoleRecorder.WriteLine($"Judge0StatusId: {detailedResult.StatusId}");
            _consoleRecorder.WriteLine(
                $"Judge0StatusDescription: {SanitizeStatusDescription(detailedResult.StatusDescription)}");
            _consoleRecorder.WriteLine($"MappedTerminalState: {mappedState}");
            _consoleRecorder.WriteLine(
                $"IsTerminal: {IsTerminal(mappedState).ToString().ToLowerInvariant()}");
            _consoleRecorder.WriteLine(
                $"ExitCode: {FormatNullableInt(detailedResult.ExitCode)}");
            _consoleRecorder.WriteLine(
                $"ExitSignal: {FormatNullableString(detailedResult.ExitSignal)}");
            _consoleRecorder.WriteLine(
                $"StdoutPresent: {(!string.IsNullOrEmpty(detailedResult.StandardOutput)).ToString().ToLowerInvariant()}");
            _consoleRecorder.WriteLine(
                $"StdoutLength: {GetLength(detailedResult.StandardOutput)}");
            _consoleRecorder.WriteLine(
                $"StderrPresent: {(!string.IsNullOrEmpty(detailedResult.StandardError)).ToString().ToLowerInvariant()}");
            _consoleRecorder.WriteLine(
                $"StderrLength: {GetLength(detailedResult.StandardError)}");
            _consoleRecorder.WriteLine(
                $"CompileOutputPresent: {(!string.IsNullOrEmpty(detailedResult.CompileOutput)).ToString().ToLowerInvariant()}");
            _consoleRecorder.WriteLine(
                $"CompileOutputLength: {GetLength(detailedResult.CompileOutput)}");
            _consoleRecorder.WriteLine(
                $"ProviderMessagePresent: {(!string.IsNullOrEmpty(detailedResult.Message)).ToString().ToLowerInvariant()}");
            _consoleRecorder.WriteLine(
                $"ProviderMessageLength: {GetLength(detailedResult.Message)}");
            _consoleRecorder.WriteLine(
                $"TimePresent: {(detailedResult.TimeSeconds.HasValue).ToString().ToLowerInvariant()}");
            _consoleRecorder.WriteLine(
                $"MemoryPresent: {(detailedResult.MemoryKb.HasValue).ToString().ToLowerInvariant()}");
            _consoleRecorder.WriteLine(
                $"ResultCaseIndex: {receipt.Tokens[0].CaseIndex}");
            _consoleRecorder.WriteLine(
                $"OutputMatched: {outputMatched.ToString().ToLowerInvariant()}");
            _consoleRecorder.WriteLine(
                $"CompileError: {(mappedState == CodeExecutionState.CompilationFailed).ToString().ToLowerInvariant()}");
            _consoleRecorder.WriteLine(
                $"RuntimeError: {(mappedState == CodeExecutionState.RuntimeFailed).ToString().ToLowerInvariant()}");
            _consoleRecorder.WriteLine(
                $"PollingRequests: {_pollRequestCount}");
            _consoleRecorder.WriteLine(
                $"LiveHttpRequests: {_httpCounter.Count}");
            _consoleRecorder.WriteLine("LiveSubmissions: 1");
            _consoleRecorder.WriteLine(
                $"TotalSmokeElapsedMilliseconds: {stopwatch.ElapsedMilliseconds}");

            var leakReport = LeakScanner.Scan(
                loggerProvider: _loggerProvider,
                consoleRecorder: _consoleRecorder,
                apiKey: apiKey,
                providerToken: providerToken,
                source: SuccessSource,
                standardInput: SuccessStandardInput,
                standardOutput: detailedResult.StandardOutput,
                standardError: detailedResult.StandardError,
                compileOutput: detailedResult.CompileOutput,
                providerMessage: detailedResult.Message);

            PrintLeakReport(
                leakReport,
                detailedDiagnostics);

            return mappedState == CodeExecutionState.ExecutionSucceeded &&
                   outputMatched &&
                   !leakReport.HasRealLeak
                ? 0
                : 1;
        }
        catch (CodeExecutionClientException exception)
        {
            stopwatch.Stop();
            _consoleRecorder.WriteLine("SubmissionCreated: failure");
            _consoleRecorder.WriteLine(
                $"ErrorCategory: {exception.ErrorCategory}");
            _consoleRecorder.WriteLine(
                $"RetryAfter: {FormatRetryAfter(exception.RetryAfter)}");
            _consoleRecorder.WriteLine(
                $"LiveHttpRequests: {_httpCounter.Count}");
            _consoleRecorder.WriteLine(
                $"TotalSmokeElapsedMilliseconds: {stopwatch.ElapsedMilliseconds}");
            return 1;
        }
    }

    public Task<int> RunCompileErrorAsync()
    {
        return RunCompileErrorCoreAsync();
    }

    private async Task<int> RunCompileErrorCoreAsync()
    {
        _loggerProvider.Clear();
        _httpCounter.Reset();
        _pollRequestCount = 0;

        var stopwatch = Stopwatch.StartNew();
        var apiKey = _configuration["Judge0:RapidApiKey"] ?? string.Empty;
        var request = CodeExecutionBatchRequest.Create(
            CompileErrorLanguageId,
            [CodeFile.Create(CompileErrorSourceFileName, CompileErrorSource)],
            [new CodeExecutionCaseRequest(0, CompileErrorStandardInput, CompileErrorTimeLimitMs, CompileErrorMemoryLimitKb)]);

        var packagingInfo = DeterminePackagingInfo(request);

        _consoleRecorder.WriteLine($"LanguageId: {CompileErrorLanguageId}");
        _consoleRecorder.WriteLine("RequestPackagingMode: single-file");
        _consoleRecorder.WriteLine($"SourceFileName: {CompileErrorSourceFileName}");
        _consoleRecorder.WriteLine($"SourceFileCount: {request.SourceFiles.Count}");
        _consoleRecorder.WriteLine($"TestcaseCount: {request.Cases.Count}");
        _consoleRecorder.WriteLine($"CaseIndex: {request.Cases[0].CaseIndex}");
        _consoleRecorder.WriteLine($"StdinCharacterCount: {CompileErrorStandardInput.Length}");
        _consoleRecorder.WriteLine($"TimeLimitMs: {request.Cases[0].TimeLimitMs}");
        _consoleRecorder.WriteLine($"MemoryLimitKb: {request.Cases[0].MemoryLimitKb}");
        _consoleRecorder.WriteLine("ExpectedOutputSentToJudge0: false");
        _consoleRecorder.WriteLine("RequestBase64Encoding: true");
        _consoleRecorder.WriteLine(
            $"UsesAdditionalFiles: {packagingInfo.UsesAdditionalFiles.ToString().ToLowerInvariant()}");
        _consoleRecorder.WriteLine(
            $"LanguageRoutedThroughMultiFileProfile89: {packagingInfo.RoutedThroughMultiFileProfile89.ToString().ToLowerInvariant()}");

        string? providerToken = null;

        try
        {
            using var deadlineCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(60));

            var receipt = await _client.CreateBatchAsync(
                request,
                deadlineCts.Token);

            if (receipt.Tokens.Count != 1)
            {
                _consoleRecorder.WriteLine("ReceiptCount: " + receipt.Tokens.Count);
                return 1;
            }

            providerToken = receipt.Tokens[0].ProviderToken;

            var detailedResult = await PollDetailedTerminalResultAsync(
                providerToken,
                deadlineCts.Token);

            var mappedState = MapState(
                detailedResult.StatusId,
                detailedResult.StatusDescription,
                detailedResult.StandardError,
                detailedResult.Message);

            stopwatch.Stop();

            _consoleRecorder.WriteLine("ReceiptCount: 1");
            _consoleRecorder.WriteLine(
                $"ReceiptCaseIndex: {receipt.Tokens[0].CaseIndex}");
            _consoleRecorder.WriteLine(
                $"ProviderTokenPresent: {(!string.IsNullOrWhiteSpace(providerToken)).ToString().ToLowerInvariant()}");
            _consoleRecorder.WriteLine($"Judge0StatusId: {detailedResult.StatusId}");
            _consoleRecorder.WriteLine(
                $"Judge0StatusDescription: {SanitizeStatusDescription(detailedResult.StatusDescription)}");
            _consoleRecorder.WriteLine($"MappedTerminalState: {mappedState}");
            _consoleRecorder.WriteLine(
                $"IsTerminal: {IsTerminal(mappedState).ToString().ToLowerInvariant()}");
            _consoleRecorder.WriteLine(
                $"ExitCode: {FormatNullableInt(detailedResult.ExitCode)}");
            _consoleRecorder.WriteLine(
                $"ExitSignal: {FormatNullableString(detailedResult.ExitSignal)}");
            _consoleRecorder.WriteLine(
                $"StdoutPresent: {(!string.IsNullOrEmpty(detailedResult.StandardOutput)).ToString().ToLowerInvariant()}");
            _consoleRecorder.WriteLine(
                $"StdoutLength: {GetLength(detailedResult.StandardOutput)}");
            _consoleRecorder.WriteLine(
                $"StderrPresent: {(!string.IsNullOrEmpty(detailedResult.StandardError)).ToString().ToLowerInvariant()}");
            _consoleRecorder.WriteLine(
                $"StderrLength: {GetLength(detailedResult.StandardError)}");
            _consoleRecorder.WriteLine(
                $"CompileOutputPresent: {(!string.IsNullOrEmpty(detailedResult.CompileOutput)).ToString().ToLowerInvariant()}");
            _consoleRecorder.WriteLine(
                $"CompileOutputLength: {GetLength(detailedResult.CompileOutput)}");
            _consoleRecorder.WriteLine(
                $"ProviderMessagePresent: {(!string.IsNullOrEmpty(detailedResult.Message)).ToString().ToLowerInvariant()}");
            _consoleRecorder.WriteLine(
                $"ProviderMessageLength: {GetLength(detailedResult.Message)}");
            _consoleRecorder.WriteLine(
                $"CompileError: {(mappedState == CodeExecutionState.CompilationFailed).ToString().ToLowerInvariant()}");
            _consoleRecorder.WriteLine(
                $"RuntimeError: {(mappedState == CodeExecutionState.RuntimeFailed).ToString().ToLowerInvariant()}");
            _consoleRecorder.WriteLine(
                $"ExecutionSuccess: {(mappedState == CodeExecutionState.ExecutionSucceeded).ToString().ToLowerInvariant()}");
            _consoleRecorder.WriteLine(
                $"PollingRequests: {_pollRequestCount}");
            _consoleRecorder.WriteLine(
                $"LiveHttpRequests: {_httpCounter.Count}");
            _consoleRecorder.WriteLine("LiveSubmissions: 1");
            _consoleRecorder.WriteLine(
                $"RetryAfter: {FormatRetryAfter(_httpCounter.LastRetryAfter)}");
            _consoleRecorder.WriteLine(
                $"TotalSmokeElapsedMilliseconds: {stopwatch.ElapsedMilliseconds}");

            var leakReport = LeakScanner.Scan(
                loggerProvider: _loggerProvider,
                consoleRecorder: _consoleRecorder,
                apiKey: apiKey,
                providerToken: providerToken,
                source: CompileErrorSource,
                standardInput: CompileErrorStandardInput,
                standardOutput: detailedResult.StandardOutput,
                standardError: detailedResult.StandardError,
                compileOutput: detailedResult.CompileOutput,
                providerMessage: detailedResult.Message);

            PrintLeakReport(
                leakReport,
                detailedDiagnostics: false);

            return mappedState == CodeExecutionState.CompilationFailed &&
                   IsTerminal(mappedState) &&
                   !leakReport.HasRealLeak
                ? 0
                : 1;
        }
        catch (CodeExecutionClientException exception)
        {
            stopwatch.Stop();
            _consoleRecorder.WriteLine("SubmissionCreated: failure");
            _consoleRecorder.WriteLine(
                $"ErrorCategory: {exception.ErrorCategory}");
            _consoleRecorder.WriteLine(
                $"RetryAfter: {FormatRetryAfter(exception.RetryAfter)}");
            _consoleRecorder.WriteLine(
                $"LiveHttpRequests: {_httpCounter.Count}");
            _consoleRecorder.WriteLine(
                $"TotalSmokeElapsedMilliseconds: {stopwatch.ElapsedMilliseconds}");
            return 1;
        }
    }

    private async Task<DetailedPollResult> PollDetailedTerminalResultAsync(
        string providerToken,
        CancellationToken cancellationToken)
    {
        var httpClient = ExtractHttpClient(_client);
        var fields = Uri.EscapeDataString(
            "token,status_id,status,stdout,stderr,compile_output,message,time,memory,exit_code,exit_signal");
        var encodedToken = Uri.EscapeDataString(providerToken);

        for (var attempt = 0; attempt < 60; attempt++)
        {
            _pollRequestCount++;
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"submissions/batch?tokens={encodedToken}&base64_encoded=true&fields={fields}");
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<Judge0DetailedBatchResponse>(
                cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException(
                    "Judge0 returned an empty detailed polling response.");

            if (body.Submissions.Count != 1)
            {
                throw new InvalidOperationException(
                    "Expected exactly one detailed polling submission.");
            }

            var item = body.Submissions[0];
            var statusId = item.StatusId != 0
                ? item.StatusId
                : item.Status?.Id ?? 0;

            var decodedStdout = DecodeNullableBase64(item.StandardOutput);
            var decodedStderr = DecodeNullableBase64(item.StandardError);
            var decodedCompileOutput = DecodeNullableBase64(item.CompileOutput);
            var decodedMessage = DecodeNullableBase64(item.Message);

            var mappedState = MapState(
                statusId,
                item.Status?.Description,
                decodedStderr,
                decodedMessage);

            if (IsTerminal(mappedState))
            {
                return new DetailedPollResult(
                    statusId,
                    item.Status?.Description,
                    mappedState,
                    decodedStdout,
                    decodedStderr,
                    decodedCompileOutput,
                    decodedMessage,
                    item.TimeSeconds,
                    item.MemoryKb,
                    item.ExitCode,
                    item.ExitSignal);
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(500),
                cancellationToken);
        }

        throw new TimeoutException(
            "The smoke test polling window expired before a terminal state was reached.");
    }

    private static HttpClient ExtractHttpClient(
        ICodeExecutionClient client)
    {
        var clientField = client.GetType().GetField(
            "_httpClient",
            BindingFlags.Instance | BindingFlags.NonPublic);

        return clientField?.GetValue(client) as HttpClient
               ?? throw new InvalidOperationException(
                   "Unable to extract the configured Judge0 HttpClient.");
    }

    private static PackagingInfo DeterminePackagingInfo(
        CodeExecutionBatchRequest request)
    {
        var usesAdditionalFiles = request.SourceFiles.Count > 1;
        var routedThroughMultiFileProfile89 =
            usesAdditionalFiles &&
            request.LanguageId is 50 or 62;

        return new PackagingInfo(
            usesAdditionalFiles,
            routedThroughMultiFileProfile89);
    }

    private static CodeExecutionState MapState(
        int statusId,
        string? statusDescription,
        string? standardError,
        string? message)
    {
        return statusId switch
        {
            1 => CodeExecutionState.Queued,
            2 => CodeExecutionState.Running,
            3 or 4 => CodeExecutionState.ExecutionSucceeded,
            5 => CodeExecutionState.TimedOut,
            6 => CodeExecutionState.CompilationFailed,
            8 => CodeExecutionState.OutputLimitExceeded,
            13 or 14 => CodeExecutionState.ProviderInternalError,
            7 or 9 or 10 or 11 or 12 =>
                ResolveRuntimeLikeState(
                    statusDescription,
                    standardError,
                    message),
            _ => CodeExecutionState.Unknown
        };
    }

    private static CodeExecutionState ResolveRuntimeLikeState(
        string? statusDescription,
        string? standardError,
        string? message)
    {
        var diagnostic = string.Join(
            ' ',
            statusDescription,
            standardError,
            message);

        if (diagnostic.Contains(
                "memory",
                StringComparison.OrdinalIgnoreCase))
        {
            return CodeExecutionState.MemoryLimitExceeded;
        }

        return CodeExecutionState.RuntimeFailed;
    }

    private static bool IsTerminal(
        CodeExecutionState state)
    {
        return state is not (
            CodeExecutionState.Queued or
            CodeExecutionState.Running);
    }

    private static string? DecodeNullableBase64(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Encoding.UTF8.GetString(
            Convert.FromBase64String(value));
    }

    private static string? NormalizeOutput(
        string? output)
    {
        return output?.Replace("\r\n", "\n").Trim();
    }

    private static string SanitizeStatusDescription(
        string? description)
    {
        return string.IsNullOrWhiteSpace(description)
            ? "unknown"
            : description.Trim();
    }

    private static string FormatRetryAfter(
        TimeSpan? retryAfter)
    {
        return retryAfter is null
            ? "none"
            : ((int)retryAfter.Value.TotalSeconds).ToString();
    }

    private static string FormatCorrelationId(
        string? correlationId)
    {
        return string.IsNullOrWhiteSpace(correlationId)
            ? "none"
            : correlationId;
    }

    private static string FormatStatusCode(
        HttpStatusCode? statusCode)
    {
        return statusCode is null
            ? "none"
            : ((int)statusCode.Value).ToString();
    }

    private static string ClassifyTransportFailure(
        HttpRequestCounter counter)
    {
        if (counter.LastExceptionType == typeof(TaskCanceledException) ||
            counter.LastExceptionType == typeof(OperationCanceledException))
        {
            return CodeExecutionErrorCategory.Timeout.ToString();
        }

        if (counter.LastExceptionType == typeof(HttpRequestException))
        {
            return CodeExecutionErrorCategory.Network.ToString();
        }

        return counter.LastStatusCode switch
        {
            HttpStatusCode.BadRequest =>
                CodeExecutionErrorCategory.Validation.ToString(),
            HttpStatusCode.Unauthorized =>
                CodeExecutionErrorCategory.Authentication.ToString(),
            HttpStatusCode.Forbidden =>
                CodeExecutionErrorCategory.Authorization.ToString(),
            HttpStatusCode.NotFound =>
                CodeExecutionErrorCategory.NotFound.ToString(),
            HttpStatusCode.RequestTimeout =>
                CodeExecutionErrorCategory.Timeout.ToString(),
            HttpStatusCode.TooManyRequests =>
                CodeExecutionErrorCategory.RateLimit.ToString(),
            { } statusCode when (int)statusCode >= 500 =>
                CodeExecutionErrorCategory.Provider.ToString(),
            _ =>
                CodeExecutionErrorCategory.Unknown.ToString()
        };
    }

    private static int GetLength(
        string? value)
    {
        return value?.Length ?? 0;
    }

    private static string FormatNullableInt(
        int? value)
    {
        return value?.ToString() ?? "null";
    }

    private static string FormatNullableString(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "null"
            : "present";
    }

    private void PrintLeakReport(
        LeakScanReport report,
        bool detailedDiagnostics)
    {
        _consoleRecorder.WriteLine(
            $"DefaultHttpClientTokenLeakDetected: {report.DefaultHttpClientTokenLeakDetected.ToString().ToLowerInvariant()}");
        _consoleRecorder.WriteLine(
            $"CustomJudge0TokenLeakDetected: {report.CustomJudge0TokenLeakDetected.ToString().ToLowerInvariant()}");
        _consoleRecorder.WriteLine(
            $"CredentialLogLeakDetected: {report.CredentialLeakDetected.ToString().ToLowerInvariant()}");
        _consoleRecorder.WriteLine(
            $"SourceLogLeakDetected: {report.SourceLogLeakDetected.ToString().ToLowerInvariant()}");
        _consoleRecorder.WriteLine(
            $"CompileDiagnosticLogLeakDetected: {report.CompileDiagnosticLogLeakDetected.ToString().ToLowerInvariant()}");
        _consoleRecorder.WriteLine(
            $"StderrLogLeakDetected: {report.StderrLogLeakDetected.ToString().ToLowerInvariant()}");
        _consoleRecorder.WriteLine(
            $"RawBodyLogLeakDetected: {report.RawBodyLeakDetected.ToString().ToLowerInvariant()}");

        if (!detailedDiagnostics)
        {
            return;
        }

        foreach (var match in report.Matches)
        {
            _consoleRecorder.WriteLine(
                $"LeakCategory: {match.LeakCategory}");
            _consoleRecorder.WriteLine(
                $"Surface: {match.Surface}");
            _consoleRecorder.WriteLine(
                $"LoggerCategory: {match.LoggerCategory}");
            _consoleRecorder.WriteLine(
                $"LogLevel: {match.LogLevel}");
            _consoleRecorder.WriteLine(
                $"EventId: {match.EventId}");
            _consoleRecorder.WriteLine(
                $"StructuredPropertyKey: {match.StructuredPropertyKey}");
            _consoleRecorder.WriteLine(
                $"MatchedValueType: {match.MatchedValueType}");
            _consoleRecorder.WriteLine(
                $"MatchedValueLength: {match.MatchedValueLength}");
            _consoleRecorder.WriteLine(
                $"MatchCount: {match.MatchCount}");
            _consoleRecorder.WriteLine(
                $"RealLeakOrFalsePositive: {(match.RealLeak ? "real leak" : "false positive")}");
            _consoleRecorder.WriteLine(
                $"Reason: {match.Reason}");
        }
    }
}

internal static class LeakScanner
{
    private const int MinimumScannableContentLength = 8;

    public static LeakScanReport Scan(
        InMemoryLoggerProvider loggerProvider,
        ConsoleRecorder consoleRecorder,
        string apiKey,
        string? providerToken,
        string source,
        string standardInput,
        string? standardOutput,
        string? standardError,
        string? compileOutput,
        string? providerMessage)
    {
        var matches = new List<LeakMatch>();
        var sourceSentinels = GetScannableSentinels(source);
        var compileDiagnosticSentinels = GetScannableSentinels(compileOutput);
        var stderrSentinels = GetScannableSentinels(standardError);
        var rawBodySentinels = GetScannableSentinels(providerMessage);
        var exceptionSentinels = GetScannableSentinels(
                apiKey,
                providerToken)
            .Concat(sourceSentinels)
            .Concat(compileDiagnosticSentinels)
            .Concat(stderrSentinels)
            .Concat(rawBodySentinels)
            .ToList();

        var loggerEntries = loggerProvider.Entries;
        foreach (var entry in loggerEntries)
        {
            FindMatches(
                matches,
                "Credential",
                apiKey,
                "logger message",
                entry.Category,
                entry.Level.ToString(),
                entry.EventId.ToString(),
                null,
                entry.Message,
                realLeak: true,
                reason: "API key appeared in a formatted log message.");

            FindMatches(
                matches,
                "ProviderToken",
                providerToken,
                "logger message",
                entry.Category,
                entry.Level.ToString(),
                entry.EventId.ToString(),
                null,
                entry.Message,
                realLeak: true,
                reason: "Provider token appeared in a formatted log message.");

            foreach (var text in sourceSentinels)
            {
                FindMatches(
                    matches,
                    "Source",
                    text,
                    "logger message",
                    entry.Category,
                    entry.Level.ToString(),
                    entry.EventId.ToString(),
                    null,
                    entry.Message,
                    realLeak: true,
                    reason: "Source content appeared in a formatted log message.");
            }

            foreach (var text in compileDiagnosticSentinels)
            {
                FindMatches(
                    matches,
                    "CompileDiagnostic",
                    text,
                    "logger message",
                    entry.Category,
                    entry.Level.ToString(),
                    entry.EventId.ToString(),
                    null,
                    entry.Message,
                    realLeak: true,
                    reason: "Compile diagnostic content appeared in a formatted log message.");
            }

            foreach (var text in stderrSentinels)
            {
                FindMatches(
                    matches,
                    "Stderr",
                    text,
                    "logger message",
                    entry.Category,
                    entry.Level.ToString(),
                    entry.EventId.ToString(),
                    null,
                    entry.Message,
                    realLeak: true,
                    reason: "Standard error content appeared in a formatted log message.");
            }

            foreach (var text in rawBodySentinels)
            {
                FindMatches(
                    matches,
                    "RawBody",
                    text,
                    "logger message",
                    entry.Category,
                    entry.Level.ToString(),
                    entry.EventId.ToString(),
                    null,
                    entry.Message,
                    realLeak: true,
                    reason: "Provider message content appeared in a formatted log message.");
            }

            foreach (var property in entry.Properties)
            {
                FindMatches(
                    matches,
                    "Credential",
                    apiKey,
                    "structured log value",
                    entry.Category,
                    entry.Level.ToString(),
                    entry.EventId.ToString(),
                    property.Key,
                    property.Value,
                    realLeak: true,
                    reason: "API key appeared in a structured log value.");

                FindMatches(
                    matches,
                    "ProviderToken",
                    providerToken,
                    "structured log value",
                    entry.Category,
                    entry.Level.ToString(),
                    entry.EventId.ToString(),
                    property.Key,
                    property.Value,
                    realLeak: true,
                    reason: "Provider token appeared in a structured log value.");

                foreach (var text in sourceSentinels)
                {
                    FindMatches(
                        matches,
                        "Source",
                        text,
                        "structured log value",
                        entry.Category,
                        entry.Level.ToString(),
                        entry.EventId.ToString(),
                        property.Key,
                        property.Value,
                        realLeak: true,
                        reason: "Source content appeared in a structured log value.");
                }

                foreach (var text in compileDiagnosticSentinels)
                {
                    FindMatches(
                        matches,
                        "CompileDiagnostic",
                        text,
                        "structured log value",
                        entry.Category,
                        entry.Level.ToString(),
                        entry.EventId.ToString(),
                        property.Key,
                        property.Value,
                        realLeak: true,
                        reason: "Compile diagnostic content appeared in a structured log value.");
                }

                foreach (var text in stderrSentinels)
                {
                    FindMatches(
                        matches,
                        "Stderr",
                        text,
                        "structured log value",
                        entry.Category,
                        entry.Level.ToString(),
                        entry.EventId.ToString(),
                        property.Key,
                        property.Value,
                        realLeak: true,
                        reason: "Standard error content appeared in a structured log value.");
                }

                foreach (var text in rawBodySentinels)
                {
                    FindMatches(
                        matches,
                        "RawBody",
                        text,
                        "structured log value",
                        entry.Category,
                        entry.Level.ToString(),
                        entry.EventId.ToString(),
                        property.Key,
                        property.Value,
                        realLeak: true,
                        reason: "Provider message content appeared in a structured log value.");
                }
            }

            foreach (var text in exceptionSentinels)
            {
                FindMatchesInException(
                    matches,
                    entry.Exception,
                    text,
                    entry.Category,
                    entry.Level.ToString(),
                    entry.EventId.ToString());
            }
        }

        foreach (var line in consoleRecorder.Lines)
        {
            FindMatches(
                matches,
                "Credential",
                apiKey,
                "console output",
                "HarnessConsole",
                "Information",
                "0",
                null,
                line,
                realLeak: true,
                reason: "API key appeared in harness console output.");

            FindMatches(
                matches,
                "ProviderToken",
                providerToken,
                "console output",
                "HarnessConsole",
                "Information",
                "0",
                null,
                line,
                realLeak: true,
                reason: "Provider token appeared in harness console output.");

            foreach (var text in sourceSentinels)
            {
                FindMatches(
                    matches,
                    "Source",
                    text,
                    "console output",
                    "HarnessConsole",
                    "Information",
                    "0",
                    null,
                    line,
                    realLeak: true,
                    reason: "Source content appeared in harness console output.");
            }

            foreach (var text in compileDiagnosticSentinels)
            {
                FindMatches(
                    matches,
                    "CompileDiagnostic",
                    text,
                    "console output",
                    "HarnessConsole",
                    "Information",
                    "0",
                    null,
                    line,
                    realLeak: true,
                    reason: "Compile diagnostic content appeared in harness console output.");
            }

            foreach (var text in stderrSentinels)
            {
                FindMatches(
                    matches,
                    "Stderr",
                    text,
                    "console output",
                    "HarnessConsole",
                    "Information",
                    "0",
                    null,
                    line,
                    realLeak: true,
                    reason: "Standard error content appeared in harness console output.");
            }

            foreach (var text in rawBodySentinels)
            {
                FindMatches(
                    matches,
                    "RawBody",
                    text,
                    "console output",
                    "HarnessConsole",
                    "Information",
                    "0",
                    null,
                    line,
                    realLeak: true,
                    reason: "Provider message content appeared in harness console output.");
            }
        }

        return new LeakScanReport(
            DefaultHttpClientTokenLeakDetected:
                matches.Any(item =>
                    item.RealLeak &&
                    item.LeakCategory == "ProviderToken" &&
                    IsDefaultJudge0HttpClientCategory(item.LoggerCategory)),
            CustomJudge0TokenLeakDetected:
                matches.Any(item =>
                    item.RealLeak &&
                    item.LeakCategory == "ProviderToken" &&
                    !IsDefaultJudge0HttpClientCategory(item.LoggerCategory)),
            CredentialLeakDetected:
                matches.Any(item => item.RealLeak && item.LeakCategory == "Credential"),
            SourceLogLeakDetected:
                matches.Any(item => item.RealLeak && item.LeakCategory == "Source"),
            CompileDiagnosticLogLeakDetected:
                matches.Any(item => item.RealLeak && item.LeakCategory == "CompileDiagnostic"),
            StderrLogLeakDetected:
                matches.Any(item => item.RealLeak && item.LeakCategory == "Stderr"),
            RawBodyLeakDetected:
                matches.Any(item => item.RealLeak && item.LeakCategory == "RawBody"),
            Matches: matches);
    }

    private static IReadOnlyList<string> GetScannableSentinels(
        params string?[] values)
    {
        return values
            .Where(IsScannableSentinel)
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static void FindMatchesInException(
        List<LeakMatch> matches,
        Exception? exception,
        string? sentinel,
        string loggerCategory,
        string logLevel,
        string eventId)
    {
        if (!IsScannableSentinel(sentinel))
        {
            return;
        }

        while (exception is not null)
        {
            FindMatches(
                matches,
                ClassifySentinel(sentinel!),
                sentinel!,
                "exception message",
                loggerCategory,
                logLevel,
                eventId,
                null,
                exception.Message,
                realLeak: true,
                reason: "Sensitive content appeared in an exception message.");

            exception = exception.InnerException;
        }
    }

    private static void FindMatches(
        List<LeakMatch> matches,
        string leakCategory,
        string? sentinel,
        string surface,
        string loggerCategory,
        string logLevel,
        string eventId,
        string? structuredPropertyKey,
        object? candidate,
        bool realLeak,
        string reason)
    {
        if (!IsScannableSentinel(sentinel) ||
            candidate is null)
        {
            return;
        }

        if (!ContainsSentinel(candidate, sentinel!, out var matchCount, out var matchedValueType))
        {
            return;
        }

        matches.Add(new LeakMatch(
            leakCategory,
            surface,
            loggerCategory,
            logLevel,
            eventId,
            structuredPropertyKey ?? "null",
            matchedValueType,
            sentinel!.Length,
            matchCount,
            realLeak,
            reason));
    }

    private static bool ContainsSentinel(
        object? candidate,
        string sentinel,
        out int matchCount,
        out string matchedValueType)
    {
        matchCount = 0;
        matchedValueType = candidate?.GetType().Name ?? "null";

        if (candidate is null)
        {
            return false;
        }

        if (candidate is string text)
        {
            matchCount = CountOccurrences(text, sentinel);
            return matchCount > 0;
        }

        if (candidate is IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                if (ContainsSentinel(item, sentinel, out var nestedCount, out var nestedType))
                {
                    matchCount += nestedCount;
                    matchedValueType = nestedType;
                }
            }

            return matchCount > 0;
        }

        var rendered = candidate.ToString();
        if (string.IsNullOrEmpty(rendered))
        {
            return false;
        }

        matchCount = CountOccurrences(rendered, sentinel);
        return matchCount > 0;
    }

    private static int CountOccurrences(
        string text,
        string sentinel)
    {
        var count = 0;
        var index = 0;

        while (true)
        {
            index = text.IndexOf(
                sentinel,
                index,
                StringComparison.Ordinal);
            if (index < 0)
            {
                return count;
            }

            count++;
            index += sentinel.Length;
        }
    }

    private static string ClassifySentinel(
        string sentinel)
    {
        return sentinel switch
        {
            { Length: > 100 } => "SourceInputOutput",
            _ => "Unknown"
        };
    }

    private static bool IsScannableSentinel(
        string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Length >= MinimumScannableContentLength;
    }

    private static bool IsDefaultJudge0HttpClientCategory(
        string category)
    {
        return string.Equals(
                   category,
                   "System.Net.Http.HttpClient.ICodeExecutionClient.LogicalHandler",
                   StringComparison.Ordinal) ||
               string.Equals(
                   category,
                   "System.Net.Http.HttpClient.ICodeExecutionClient.ClientHandler",
                   StringComparison.Ordinal);
    }
}

internal sealed record LeakScanReport(
    bool DefaultHttpClientTokenLeakDetected,
    bool CustomJudge0TokenLeakDetected,
    bool CredentialLeakDetected,
    bool SourceLogLeakDetected,
    bool CompileDiagnosticLogLeakDetected,
    bool StderrLogLeakDetected,
    bool RawBodyLeakDetected,
    IReadOnlyList<LeakMatch> Matches)
{
    public bool HasRealLeak =>
        DefaultHttpClientTokenLeakDetected ||
        CustomJudge0TokenLeakDetected ||
        CredentialLeakDetected ||
        SourceLogLeakDetected ||
        CompileDiagnosticLogLeakDetected ||
        StderrLogLeakDetected ||
        RawBodyLeakDetected;
}

internal sealed record LeakMatch(
    string LeakCategory,
    string Surface,
    string LoggerCategory,
    string LogLevel,
    string EventId,
    string StructuredPropertyKey,
    string MatchedValueType,
    int MatchedValueLength,
    int MatchCount,
    bool RealLeak,
    string Reason);

internal sealed record PackagingInfo(
    bool UsesAdditionalFiles,
    bool RoutedThroughMultiFileProfile89);

internal sealed class ConsoleRecorder
{
    private readonly List<string> _lines = [];

    public IReadOnlyList<string> Lines => _lines;

    public void WriteLine(
        string value)
    {
        _lines.Add(value);
        Console.WriteLine(value);
    }
}

internal sealed class HarnessHostEnvironment
    : IHostEnvironment
{
    public HarnessHostEnvironment(
        string environmentName)
    {
        EnvironmentName = environmentName;
    }

    public string EnvironmentName { get; set; }

    public string ApplicationName { get; set; } =
        "Judge0Smoke";

    public string ContentRootPath { get; set; } =
        Directory.GetCurrentDirectory();

    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
        new Microsoft.Extensions.FileProviders.NullFileProvider();
}

internal sealed class HttpRequestCounter
{
    private int _count;

    public int Count => _count;

    public HttpStatusCode? LastStatusCode { get; private set; }

    public TimeSpan? LastRetryAfter { get; private set; }

    public string? LastCorrelationId { get; private set; }

    public Type? LastExceptionType { get; private set; }

    public void Increment()
    {
        Interlocked.Increment(ref _count);
    }

    public void RecordResponse(
        HttpResponseMessage response)
    {
        LastStatusCode = response.StatusCode;
        LastRetryAfter = response.Headers.RetryAfter?.Delta;
        LastCorrelationId = TryGetCorrelationId(response);
        LastExceptionType = null;
    }

    public void RecordException(
        Exception exception)
    {
        LastExceptionType = exception.GetType();
        LastStatusCode = null;
        LastRetryAfter = null;
        LastCorrelationId = null;
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _count, 0);
        LastStatusCode = null;
        LastRetryAfter = null;
        LastCorrelationId = null;
        LastExceptionType = null;
    }

    private static string? TryGetCorrelationId(
        HttpResponseMessage response)
    {
        foreach (var headerName in new[] { "X-Request-Id", "Request-Id", "Trace-Id" })
        {
            if (response.Headers.TryGetValues(headerName, out var values))
            {
                var value = values.FirstOrDefault()?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }
}

internal sealed class CountingHandler
    : DelegatingHandler
{
    private readonly HttpRequestCounter _counter;

    public CountingHandler(
        HttpRequestCounter counter)
    {
        _counter = counter;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _counter.Increment();
        return SendAndTrackAsync(
            request,
            cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAndTrackAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await base.SendAsync(
                request,
                cancellationToken);
            _counter.RecordResponse(response);
            return response;
        }
        catch (Exception exception)
        {
            _counter.RecordException(exception);
            throw;
        }
    }
}

internal sealed class CountingHttpMessageHandlerBuilderFilter
    : IHttpMessageHandlerBuilderFilter
{
    private readonly HttpRequestCounter _counter;

    public CountingHttpMessageHandlerBuilderFilter(
        HttpRequestCounter counter)
    {
        _counter = counter;
    }

    public Action<HttpMessageHandlerBuilder> Configure(
        Action<HttpMessageHandlerBuilder> next)
    {
        return builder =>
        {
            next(builder);
            builder.AdditionalHandlers.Add(
                new CountingHandler(_counter));
        };
    }
}

internal sealed class InMemoryLoggerProvider
    : ILoggerProvider
{
    private readonly List<LogEntry> _entries = [];
    private readonly object _gate = new();

    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return _entries.ToList();
            }
        }
    }

    public ILogger CreateLogger(
        string categoryName)
    {
        return new InMemoryLogger(
            categoryName,
            this);
    }

    public void Dispose()
    {
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }
    }

    public void AddEntry(
        LogEntry entry)
    {
        lock (_gate)
        {
            _entries.Add(entry);
        }
    }
}

internal sealed class InMemoryLogger
    : ILogger
{
    private readonly string _categoryName;
    private readonly InMemoryLoggerProvider _provider;

    public InMemoryLogger(
        string categoryName,
        InMemoryLoggerProvider provider)
    {
        _categoryName = categoryName;
        _provider = provider;
    }

    public IDisposable BeginScope<TState>(
        TState state)
        where TState : notnull
    {
        return NullScope.Instance;
    }

    public bool IsEnabled(
        LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (state is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item is KeyValuePair<string, object?> pair)
                {
                    properties[pair.Key] = pair.Value;
                }
            }
        }

        _provider.AddEntry(
            new LogEntry(
                _categoryName,
                logLevel,
                eventId,
                formatter(state, exception),
                properties,
                exception));
    }
}

internal sealed class NullScope
    : IDisposable
{
    public static NullScope Instance { get; } = new();

    public void Dispose()
    {
    }
}

internal sealed record LogEntry(
    string Category,
    LogLevel Level,
    EventId EventId,
    string Message,
    IReadOnlyDictionary<string, object?> Properties,
    Exception? Exception);

internal sealed record LanguageInfo
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("is_archived")]
    public bool Archived { get; init; }
}

internal sealed record Judge0DetailedBatchResponse
{
    [JsonPropertyName("submissions")]
    public List<Judge0DetailedSubmissionResponse> Submissions
    {
        get;
        init;
    } = [];
}

internal sealed record Judge0DetailedSubmissionResponse
{
    [JsonPropertyName("status_id")]
    public int StatusId { get; init; }

    [JsonPropertyName("status")]
    public Judge0DetailedStatusResponse? Status { get; init; }

    [JsonPropertyName("stdout")]
    public string? StandardOutput { get; init; }

    [JsonPropertyName("stderr")]
    public string? StandardError { get; init; }

    [JsonPropertyName("compile_output")]
    public string? CompileOutput { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("time")]
    public double? TimeSeconds { get; init; }

    [JsonPropertyName("memory")]
    public int? MemoryKb { get; init; }

    [JsonPropertyName("exit_code")]
    public int? ExitCode { get; init; }

    [JsonPropertyName("exit_signal")]
    public string? ExitSignal { get; init; }
}

internal sealed record Judge0DetailedStatusResponse
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

internal sealed record DetailedPollResult(
    int StatusId,
    string? StatusDescription,
    CodeExecutionState MappedState,
    string? StandardOutput,
    string? StandardError,
    string? CompileOutput,
    string? Message,
    double? TimeSeconds,
    int? MemoryKb,
    int? ExitCode,
    string? ExitSignal);
