using Application.DTOs;

namespace Application.Interfaces;

public interface IGoogleAuthService
{
    Task<GoogleUserPayload> VerifyGoogleTokenAsync(string idToken);
        
}