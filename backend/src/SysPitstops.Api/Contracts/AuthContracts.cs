using System.ComponentModel.DataAnnotations;

namespace SysPitstops.Api.Contracts;

public record LoginRequest
{
    [Required]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public record AuthenticatedUser(
    Guid Id,
    string Name,
    string Email,
    string Role,
    int WorkshopId);
