namespace Infrastructure.Auth;

public sealed class GoogleAuthConfig
{
    public const string SectionName = "GoogleAuth";

    public string ClientId { get; init; } = string.Empty;
}