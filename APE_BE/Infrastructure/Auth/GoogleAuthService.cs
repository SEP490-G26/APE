using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Auth;

public sealed class GoogleAuthService : IGoogleAuthService
{
    private readonly GoogleAuthConfig _config;
    private readonly IGoogleTokenValidator _tokenValidator;
    private readonly ILogger<GoogleAuthService> _logger;

    public GoogleAuthService(
        IOptions<GoogleAuthConfig> options,
        IGoogleTokenValidator tokenValidator,
        ILogger<GoogleAuthService> logger)
    {
        _config = options.Value;
        _tokenValidator = tokenValidator;
        _logger = logger;
    }

    public async Task<GoogleUserPayload> VerifyGoogleTokenAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new ValidationException("Google ID Token is required.");
        }

        GoogleJsonWebSignature.Payload payload;

        try
        {
            payload = await _tokenValidator.ValidateAsync(
                idToken,
                new[]
                {
                    _config.ClientId
                });
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning(ex, "Invalid Google ID Token.");

            throw new UnauthorizedException("Invalid Google ID Token.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google authentication failed.");

            throw new UnauthorizedException("Google authentication failed.");
        }

        if (!payload.EmailVerified)
        {
            throw new UnauthorizedException("Google email is not verified.");
        }

        if (!string.Equals(
                payload.Issuer,
                "https://accounts.google.com",
                StringComparison.OrdinalIgnoreCase)
            &&
            !string.Equals(
                payload.Issuer,
                "accounts.google.com",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedException("Invalid Google issuer.");
        }

        if (string.IsNullOrWhiteSpace(payload.Subject))
        {
            throw new UnauthorizedException("Google subject is required.");
        }

        if (string.IsNullOrWhiteSpace(payload.Email))
        {
            throw new UnauthorizedException("Google email is required.");
        }

        return new GoogleUserPayload
        {
            Sub = payload.Subject,
            Email = payload.Email,
            Name = payload.Name,
            Picture = payload.Picture
        };
    }
}
