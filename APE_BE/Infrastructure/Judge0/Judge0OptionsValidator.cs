using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Infrastructure.Judge0;

public sealed class Judge0OptionsValidator
    : IValidateOptions<Judge0Options>
{
    private const int MaximumTimeoutSeconds = 300;
    private static readonly string[] UnsafePlaceholders =
    [
        "changeme",
        "change-me",
        "replace-me",
        "your-api-key",
        "your-secret-key",
        "placeholder",
        "<api-key>",
        "<rapidapi-key>",
        "<auth-token>",
        "<set-via-user-secrets-or-environment>"
    ];

    private readonly IHostEnvironment _hostEnvironment;
    private readonly IConfiguration _configuration;

    public Judge0OptionsValidator(
        IHostEnvironment hostEnvironment,
        IConfiguration configuration)
    {
        _hostEnvironment = hostEnvironment;
        _configuration = configuration;
    }

    public ValidateOptionsResult Validate(
        string? name,
        Judge0Options options)
    {
        var errors = new List<string>();
        var hasValidBaseUri = TryValidateBaseUri(
            options.BaseUrl,
            errors,
            out var baseUri);
        var rawAuthenticationMode = _configuration[
            $"{Judge0Options.SectionName}:{nameof(Judge0Options.AuthenticationMode)}"];
        var hasValidAuthenticationMode = TryValidateAuthenticationMode(
            rawAuthenticationMode,
            errors,
            out var authenticationMode);

        if (options.TimeoutSeconds <= 0 ||
            options.TimeoutSeconds > MaximumTimeoutSeconds)
        {
            errors.Add(
                $"Judge0:TimeoutSeconds must be between 1 and {MaximumTimeoutSeconds}.");
        }

        if (options.MaximumBatchSize <= 0)
        {
            errors.Add(
                "Judge0:MaximumBatchSize must be greater than zero.");
        }

        if (options.MaximumSourceFiles <= 0)
        {
            errors.Add(
                "Judge0:MaximumSourceFiles must be greater than zero.");
        }

        if (options.MaximumArchiveBytes <= 0)
        {
            errors.Add(
                "Judge0:MaximumArchiveBytes must be greater than zero.");
        }

        if (options.MultiFileLanguageId <= 0)
        {
            errors.Add(
                "Judge0:MultiFileLanguageId must be greater than zero.");
        }

        if (hasValidAuthenticationMode)
        {
            switch (authenticationMode)
            {
                case Judge0AuthenticationMode.RapidApi:
                    ValidateRapidApi(
                        options,
                        hasValidBaseUri ? baseUri : null,
                        errors);
                    break;

                case Judge0AuthenticationMode.Judge0AuthToken:
                    if (IsMissingOrUnsafe(options.AuthToken))
                    {
                        errors.Add(
                            "Judge0 auth token is missing or uses an unsafe placeholder.");
                    }

                    break;

                case Judge0AuthenticationMode.None:
                    break;
            }
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }

    private static bool TryValidateAuthenticationMode(
        string? rawAuthenticationMode,
        List<string> errors,
        out Judge0AuthenticationMode authenticationMode)
    {
        authenticationMode = default;

        var normalizedAuthenticationMode = rawAuthenticationMode?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedAuthenticationMode) ||
            int.TryParse(
                normalizedAuthenticationMode,
                out _) ||
            !Enum.TryParse<Judge0AuthenticationMode>(
                normalizedAuthenticationMode,
                ignoreCase: true,
                out authenticationMode) ||
            !Enum.GetNames<Judge0AuthenticationMode>()
                .Any(allowedMode =>
                    string.Equals(
                        allowedMode,
                        normalizedAuthenticationMode,
                        StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add(
                "Judge0 authentication mode must be explicitly configured as None, RapidApi, or Judge0AuthToken.");
            return false;
        }

        return true;
    }

    private bool TryValidateBaseUri(
        string baseUrl,
        List<string> errors,
        out Uri? baseUri)
    {
        baseUri = null;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            errors.Add("Judge0:BaseUrl is required.");
            return false;
        }

        if (IsUnsafePlaceholder(baseUrl))
        {
            errors.Add(
                "Judge0:BaseUrl is missing or uses an unsafe placeholder.");
            return false;
        }

        if (!Uri.TryCreate(
                baseUrl.Trim(),
                UriKind.Absolute,
                out var parsedBaseUri) ||
            parsedBaseUri.Scheme is not ("http" or "https"))
        {
            errors.Add(
                "Judge0:BaseUrl must be an absolute HTTP or HTTPS URL.");
            return false;
        }

        if (!string.IsNullOrEmpty(parsedBaseUri.UserInfo))
        {
            errors.Add(
                "Judge0:BaseUrl must not include user information.");
        }

        if (!string.IsNullOrEmpty(parsedBaseUri.Query))
        {
            errors.Add(
                "Judge0:BaseUrl must not include a query string.");
        }

        if (!string.IsNullOrEmpty(parsedBaseUri.Fragment))
        {
            errors.Add(
                "Judge0:BaseUrl must not include a fragment.");
        }

        if (!_hostEnvironment.IsDevelopment() &&
            !string.Equals(
                parsedBaseUri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                "Judge0:BaseUrl must use HTTPS outside Development.");
        }

        baseUri = parsedBaseUri;
        return true;
    }

    private static void ValidateRapidApi(
        Judge0Options options,
        Uri? baseUri,
        List<string> errors)
    {
        if (IsMissingOrUnsafe(options.RapidApiKey))
        {
            errors.Add(
                "Judge0 RapidAPI key is missing or uses an unsafe placeholder.");
        }

        if (string.IsNullOrWhiteSpace(options.RapidApiHost))
        {
            errors.Add(
                "Judge0 RapidAPI host is required.");
            return;
        }

        if (IsUnsafePlaceholder(options.RapidApiHost))
        {
            errors.Add(
                "Judge0 RapidAPI host is missing or uses an unsafe placeholder.");
            return;
        }

        if (!TryValidateRapidApiHost(
                options.RapidApiHost,
                out var normalizedRapidApiHost))
        {
            errors.Add(
                "Judge0 RapidAPI host must be a hostname only.");
            return;
        }

        if (baseUri is not null &&
            !string.Equals(
                normalizedRapidApiHost,
                NormalizeHostForComparison(
                    baseUri.Host),
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                "Judge0 RapidAPI host must match Judge0:BaseUrl host.");
        }
    }

    private static bool IsMissingOrUnsafe(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value) ||
               IsUnsafePlaceholder(value);
    }

    private static bool IsUnsafePlaceholder(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return UnsafePlaceholders.Contains(
            value.Trim(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryValidateRapidApiHost(
        string value,
        out string normalizedHost)
    {
        normalizedHost = string.Empty;

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

        normalizedHost = NormalizeHostForComparison(
            trimmed);
        return true;
    }

    private static string NormalizeHostForComparison(
        string host)
    {
        return host
            .Trim()
            .TrimEnd('.')
            .ToLowerInvariant();
    }
}
