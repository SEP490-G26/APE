namespace Infrastructure.Judge0;

public sealed class Judge0Options
{
    public const string SectionName = "Judge0";
    private static readonly string[] AllowedAuthenticationModes =
    [
        nameof(Judge0AuthenticationMode.None),
        nameof(Judge0AuthenticationMode.RapidApi),
        nameof(Judge0AuthenticationMode.Judge0AuthToken)
    ];

    public string BaseUrl { get; init; } = string.Empty;

    public string AuthenticationMode { get; init; } = string.Empty;

    public int TimeoutSeconds { get; init; } = 30;

    public int MaximumBatchSize { get; init; } = 100;

    public int MaximumSourceFiles { get; init; } = 20;

    public int MaximumArchiveBytes { get; init; } =
        5 * 1024 * 1024;

    public int MultiFileLanguageId { get; init; } = 89;

    public string? RapidApiKey { get; init; }

    public string? RapidApiHost { get; init; }

    public string? AuthToken { get; init; }

    public bool TryGetAuthenticationMode(
        out Judge0AuthenticationMode mode)
    {
        mode = default;

        var rawMode = AuthenticationMode?.Trim();
        if (string.IsNullOrWhiteSpace(rawMode) ||
            IsNumeric(rawMode))
        {
            return false;
        }

        var matchedMode = AllowedAuthenticationModes
            .FirstOrDefault(allowedMode =>
                string.Equals(
                    allowedMode,
                    rawMode,
                    StringComparison.OrdinalIgnoreCase));

        if (matchedMode is null)
        {
            return false;
        }

        mode = matchedMode switch
        {
            nameof(Judge0AuthenticationMode.None) =>
                Judge0AuthenticationMode.None,
            nameof(Judge0AuthenticationMode.RapidApi) =>
                Judge0AuthenticationMode.RapidApi,
            nameof(Judge0AuthenticationMode.Judge0AuthToken) =>
                Judge0AuthenticationMode.Judge0AuthToken,
            _ => throw new InvalidOperationException(
                "Unsupported Judge0 authentication mode.")
        };

        return true;
    }

    public Judge0AuthenticationMode GetAuthenticationModeOrThrow()
    {
        return TryGetAuthenticationMode(out var mode)
            ? mode
            : throw new InvalidOperationException(
                "Judge0 authentication mode must be explicitly configured as None, RapidApi, or Judge0AuthToken.");
    }

    private static bool IsNumeric(
        string value)
    {
        return int.TryParse(
            value,
            out _);
    }
}
