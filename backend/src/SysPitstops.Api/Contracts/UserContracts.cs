using System.ComponentModel.DataAnnotations;
using SysPitstops.Api.Domain;

namespace SysPitstops.Api.Contracts;

public record CreateUserRequest
{
    [Required(ErrorMessage = "O nome é obrigatório.")]
    [StringLength(120, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [Required(ErrorMessage = "O e-mail é obrigatório.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [StringLength(160)]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "A senha é obrigatória.")]
    [StringLength(100, MinimumLength = 8,
        ErrorMessage = "A senha deve ter pelo menos 8 caracteres.")]
    public string Password { get; init; } = string.Empty;

    [Required]
    public UserRole Role { get; init; }
}

public record UserResponse(
    Guid Id,
    string Name,
    string Email,
    UserRole Role,
    bool IsActive,
    DateTimeOffset CreatedAt);
