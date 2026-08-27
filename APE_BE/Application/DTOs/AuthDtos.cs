using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;



public sealed class GoogleLoginRequest
{
    [Required]
    public string IdToken { get; set; } = null!;
}
public class AuthResponse
{
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public UserProfileDto User { get; set; } = null!;
}

public sealed class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = null!;
}



public class UpdateProfileDto
{
    [MaxLength(100)]
    public string? FullName { get; set; }

    [Url]
    [MaxLength(500)]
    public string? AvatarUrl { get; set; }
}

public class GoogleUserPayload
{
    public string Sub { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Picture { get; set; } = null!;
}