using System.ComponentModel.DataAnnotations;

namespace SysPitstops.Api.Contracts;

public record CustomerRequest
{
    [Required(ErrorMessage = "O nome é obrigatório.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 120 caracteres.")]
    public string Name { get; init; } = string.Empty;

    [Required(ErrorMessage = "O telefone é obrigatório.")]
    [StringLength(20)]
    public string Phone { get; init; } = string.Empty;

    [StringLength(18)]
    public string? Document { get; init; }

    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [StringLength(160)]
    public string? Email { get; init; }

    [StringLength(1000)]
    public string? Notes { get; init; }
}

public record CustomerResponse(
    Guid Id,
    string Name,
    string Phone,
    string? Document,
    string? Email,
    string? Notes,
    bool IsActive,
    int VehicleCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
