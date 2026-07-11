using AlFalah.Domain.Enums;

namespace AlFalah.Application.DTOs.Auth;

/// <summary>
/// Request for school-scoped user login.
/// User must select a school, then provide credentials.
/// </summary>
public class SchoolLoginRequestDto
{
    public int SchoolId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
