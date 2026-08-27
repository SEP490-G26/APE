using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BE_IntegrationTests.Infrastructure;

public sealed class IntegrationTestAuthHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "IntegrationTest";
    public const string UserIdHeader = "X-Test-UserId";
    public const string RoleHeader = "X-Test-Role";
    public const string EmailHeader = "X-Test-Email";
    public const string NameHeader = "X-Test-Name";

    public IntegrationTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeader, out var userId) ||
            string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var role = Request.Headers[RoleHeader].FirstOrDefault() ?? "Student";
        var email = Request.Headers[EmailHeader].FirstOrDefault() ?? "integration-user@ape.local";
        var name = Request.Headers[NameHeader].FirstOrDefault() ?? email;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, name)
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
