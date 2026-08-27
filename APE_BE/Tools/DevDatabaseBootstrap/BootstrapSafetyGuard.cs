namespace DevDatabaseBootstrap;

public static class BootstrapSafetyGuard
{
    private static readonly HashSet<string> ProhibitedDatabaseNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "admin",
            "config",
            "local"
        };

    public static SafetyGuardResult Evaluate(
        string command,
        IReadOnlyCollection<string> arguments,
        string? aspNetCoreEnvironment,
        string? dotNetEnvironment,
        string databaseName)
    {
        var hasConfirmationFlag = arguments.Contains(
            "--confirm-development-database",
            StringComparer.Ordinal);

        if (!hasConfirmationFlag)
        {
            return SafetyGuardResult.Fail(
                "The --confirm-development-database flag is required.");
        }

        if (ProhibitedDatabaseNames.Contains(databaseName.Trim()))
        {
            return SafetyGuardResult.Fail(
                $"Database '{databaseName}' is prohibited for bootstrap commands.");
        }

        var environmentName =
            !string.IsNullOrWhiteSpace(aspNetCoreEnvironment)
                ? aspNetCoreEnvironment
                : dotNetEnvironment;

        if (!string.Equals(
                environmentName,
                "Development",
                StringComparison.OrdinalIgnoreCase))
        {
            return SafetyGuardResult.Fail(
                "Bootstrap commands are allowed only when ASPNETCORE_ENVIRONMENT or DOTNET_ENVIRONMENT is Development.");
        }

        return SafetyGuardResult.Allow();
    }
}

public sealed record SafetyGuardResult(
    bool IsAllowed,
    string? ErrorMessage)
{
    public static SafetyGuardResult Allow() =>
        new(true, null);

    public static SafetyGuardResult Fail(string message) =>
        new(false, message);
}
