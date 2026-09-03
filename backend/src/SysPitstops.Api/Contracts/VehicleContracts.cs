using System.ComponentModel.DataAnnotations;

namespace SysPitstops.Api.Contracts;

public record VehicleRequest
{
    [Required(ErrorMessage = "O cliente é obrigatório.")]
    public Guid OwnerId { get; init; }

    [Required(ErrorMessage = "A placa é obrigatória.")]
    [StringLength(10)]
    public string Plate { get; init; } = string.Empty;

    [Required(ErrorMessage = "A marca é obrigatória.")]
    [StringLength(60)]
    public string Brand { get; init; } = string.Empty;

    [Required(ErrorMessage = "O modelo é obrigatório.")]
    [StringLength(60)]
    public string Model { get; init; } = string.Empty;

    public int? ModelYear { get; init; }

    [StringLength(30)]
    public string? Color { get; init; }

    [StringLength(17)]
    public string? Vin { get; init; }
}

public record VehicleResponse(
    Guid Id,
    Guid OwnerId,
    string OwnerName,
    string Plate,
    string Brand,
    string Model,
    int? ModelYear,
    string? Color,
    string? Vin,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
