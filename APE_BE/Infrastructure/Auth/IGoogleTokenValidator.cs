using Google.Apis.Auth;

namespace Infrastructure.Auth;

public interface IGoogleTokenValidator
{
    Task<GoogleJsonWebSignature.Payload> ValidateAsync(
        string idToken,
        IEnumerable<string> audience);
}
