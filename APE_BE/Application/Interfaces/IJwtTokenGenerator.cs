using System.Security.Claims;
using Domain.Entities;

namespace Application.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(User user);

    (string Token, DateTime Expires) GenerateRefreshTokenWithExpiry();

    ClaimsPrincipal GetPrincipalFromExpiredToken(string accessToken);
}