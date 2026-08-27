using Google.Apis.Auth;

namespace Infrastructure.Auth;

public sealed class GoogleTokenValidator : IGoogleTokenValidator
{
    public Task<GoogleJsonWebSignature.Payload> ValidateAsync(
        string idToken,
        IEnumerable<string> audience)
    {
        return GoogleJsonWebSignature.ValidateAsync(
            idToken,
            new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = audience
            });
    }
}
