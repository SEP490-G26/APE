using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public class AdminUserListDto
{
    public string Id { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = null!;
    public string Status { get; set; } = "Active";
   
}

public class AdminUserDetailDto
{
    public string Id { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = null!;
    public string Status { get; set; } = "Active";
}

public class UpdateUserStatusDto
{
    [Required]
    public string Status { get; set; } = null!;
}
