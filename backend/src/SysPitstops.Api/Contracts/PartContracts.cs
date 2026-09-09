using System.ComponentModel.DataAnnotations;
using SysPitstops.Api.Domain;

namespace SysPitstops.Api.Contracts;

/// <summary>
/// Note the absence of a quantity: <c>quantity_on_hand</c> is never written by a
/// form. Section 4 of data-model.md makes the movement the truth and the balance
/// a cache of it, so stock only changes through /api/parts/{id}/movements.
/// </summary>
public record PartRequest
{
    [Required(ErrorMessage = "O código da peça é obrigatório.")]
    [StringLength(40, MinimumLength = 1, ErrorMessage = "O código deve ter até 40 caracteres.")]
    public string Sku { get; init; } = string.Empty;

    [Required(ErrorMessage = "O nome é obrigatório.")]
    [StringLength(160, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 160 caracteres.")]
    public string Name { get; init; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; init; }

    /// <summary>UN, L, KG, PC… Livre de propósito: a oficina compra em unidades
    /// que o MVP não tenta catalogar.</summary>
    [Required(ErrorMessage = "A unidade é obrigatória.")]
    [StringLength(10, MinimumLength = 1, ErrorMessage = "A unidade deve ter até 10 caracteres.")]
    public string Unit { get; init; } = "UN";

    [Range(0, 999_999_999, ErrorMessage = "O preço de venda não pode ser negativo.")]
    public decimal SalePrice { get; init; }

    [Range(0, 999_999_999, ErrorMessage = "O preço de custo não pode ser negativo.")]
    public decimal CostPrice { get; init; }

    /// <summary>Abaixo disto a peça aparece como estoque baixo. Zero desliga o aviso.</summary>
    [Range(0, 9_999_999, ErrorMessage = "O estoque mínimo não pode ser negativo.")]
    public decimal MinQuantity { get; init; }

    [StringLength(60)]
    public string? Location { get; init; }
}

/// <summary>
/// For IN and OUT, <see cref="Quantity"/> is how much moved. For ADJUSTMENT it
/// is the counted balance — see the comment on PartsController.Apply.
/// </summary>
public record StockMovementRequest
{
    [Required(ErrorMessage = "O tipo de movimento é obrigatório.")]
    public MovementType MovementType { get; init; }

    [Range(0, 9_999_999, ErrorMessage = "A quantidade não pode ser negativa.")]
    public decimal Quantity { get; init; }

    [Range(0, 999_999_999, ErrorMessage = "O custo unitário não pode ser negativo.")]
    public decimal? UnitCost { get; init; }

    [StringLength(500)]
    public string? Note { get; init; }
}

// ---------------------------------------------------------------------------
// Responses
// ---------------------------------------------------------------------------

public record PartResponse(
    Guid Id,
    string Sku,
    string Name,
    string? Description,
    string Unit,
    decimal SalePrice,
    decimal CostPrice,
    decimal QuantityOnHand,
    decimal MinQuantity,
    string? Location,
    bool IsActive,
    // Computed, not stored: the balance reached the minimum.
    bool IsLowStock,
    // A negative balance means more was written off than was ever entered — the
    // risk D-11 accepted, surfaced instead of hidden.
    bool IsNegative,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record StockMovementResponse(
    Guid Id,
    Guid PartId,
    MovementType MovementType,
    decimal Quantity,
    decimal? UnitCost,
    Guid? ServiceOrderId,
    int? ServiceOrderNumber,
    Guid UserId,
    string UserName,
    string? Note,
    DateTimeOffset CreatedAt);
