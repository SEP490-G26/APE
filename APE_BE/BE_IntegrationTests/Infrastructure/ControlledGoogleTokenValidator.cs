using Google.Apis.Auth;
using Infrastructure.Auth;

namespace BE_IntegrationTests.Infrastructure;

public sealed class ControlledGoogleTokenValidator : IGoogleTokenValidator
{
    private readonly Dictionary<string, GoogleJsonWebSignature.Payload> _payloads =
        new(StringComparer.Ordinal);

    public void Register(
        string idToken,
        GoogleJsonWebSignature.Payload payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idToken);
        ArgumentNullException.ThrowIfNull(payload);

        _payloads[idToken] = payload;
    }

    public void Reset()
    {
        _payloads.Clear();
    }

    public Task<GoogleJsonWebSignature.Payload> ValidateAsync(
        string idToken,
        IEnumerable<string> audience)
    {
        if (_payloads.TryGetValue(idToken, out var payload))
        {
            return Task.FromResult(payload);
        }

        throw new InvalidJwtException("Integration test token was not registered.");
    }
}
