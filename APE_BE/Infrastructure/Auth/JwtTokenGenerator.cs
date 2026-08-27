using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Auth;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _settings;
    private readonly ILogger<JwtTokenGenerator> _logger;

    public JwtTokenGenerator(
        IOptions<JwtSettings> options,
        ILogger<JwtTokenGenerator> logger)
    {
        _settings = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_settings.Secret))
        {
            throw new InvalidOperationException(
                "JwtSettings:Secret is required.");
        }

        if (Encoding.UTF8.GetByteCount(_settings.Secret) < 32)
        {
            throw new InvalidOperationException(
                "JwtSettings:Secret must be at least 32 bytes for HMAC-SHA256.");
        }

        if (_settings.AccessTokenExpirationMinutes <= 0)
        {
            throw new InvalidOperationException(
                "JwtSettings:AccessTokenExpirationMinutes must be greater than zero.");
        }

        if (_settings.RefreshTokenExpirationDays <= 0)
        {
            throw new InvalidOperationException(
                "JwtSettings:RefreshTokenExpirationDays must be greater than zero.");
        }
    }

    public string GenerateAccessToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(user.Id))
        {
            throw new ArgumentException(
                "User id is required.",
                nameof(user));
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            throw new ArgumentException(
                "User email is required.",
                nameof(user));
        }

        if (string.IsNullOrWhiteSpace(user.Role))
        {
            throw new ArgumentException(
                "User role is required.",
                nameof(user));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_settings.Secret)),
            SecurityAlgorithms.HmacSha256);

        var claims = BuildClaims(user);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(
                _settings.AccessTokenExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (string Token, DateTime Expires) GenerateRefreshTokenWithExpiry()
    {
        Span<byte> bytes = stackalloc byte[64];

        RandomNumberGenerator.Fill(bytes);

        return
        (
            Convert.ToBase64String(bytes),
            DateTime.UtcNow.AddDays(
                _settings.RefreshTokenExpirationDays)
        );
    }

    public ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new SecurityTokenException("Token is missing.");

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = false,

            ValidIssuer = _settings.Issuer,
            ValidAudience = _settings.Audience,

            IssuerSigningKey =
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(_settings.Secret))
        };

        var handler = new JwtSecurityTokenHandler();

        var principal = handler.ValidateToken(
            token,
            validationParameters,
            out var securityToken);

        if (securityToken is not JwtSecurityToken jwt ||
            !jwt.Header.Alg.Equals(
                SecurityAlgorithms.HmacSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityTokenException("Invalid JWT token.");
        }

        return principal;
    }

    private static IEnumerable<Claim> BuildClaims(User user)
    {
        yield return new Claim(
            ClaimTypes.NameIdentifier,
            user.Id);

        yield return new Claim(
            ClaimTypes.Email,
            user.Email);

        if (!string.IsNullOrWhiteSpace(user.FullName))
        {
            yield return new Claim(
                ClaimTypes.Name,
                user.FullName);
        }

        yield return new Claim(
            ClaimTypes.Role,
            user.Role);

        yield return new Claim(
            JwtRegisteredClaimNames.Sub,
            user.Id);

        yield return new Claim(
            JwtRegisteredClaimNames.Email,
            user.Email);

        yield return new Claim(
            JwtRegisteredClaimNames.Jti,
            Guid.NewGuid().ToString());
    }
}
