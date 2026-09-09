using System.ComponentModel.DataAnnotations;
using SysPitstops.Api.Domain;

namespace SysPitstops.Api.Contracts;

// ---------------------------------------------------------------------------
// Requests
// ---------------------------------------------------------------------------

/// <summary>The customer is not sent: it is copied from the vehicle owner at
/// opening time and frozen on the order (D-08).</summary>
public record OpenServiceOrderRequest
{
    [Required]
    public Guid VehicleId { get; init; }

    /// <summary>Opcional: o carro chega antes de alguém assumir. A partir de
    /// IN_YARD a ordem passa a exigir responsável (D-38).</summary>
    public Guid? MechanicId { get; init; }

    [Range(0, 9_999_999, ErrorMessage = "Quilometragem inválida.")]
    public int? Mileage { get; init; }

    [StringLength(2000)]
    public string? ReportedIssue { get; init; }

    public DateTimeOffset? ScheduledAt { get; init; }
}

public record UpdateServiceOrderRequest
{
    /// <summary>One responsible mechanic per order (D-12). Null unassigns.</summary>
    public Guid? MechanicId { get; init; }

    [Range(0, 9_999_999, ErrorMessage = "Quilometragem inválida.")]
    public int? Mileage { get; init; }

    [StringLength(2000)]
    public string? ReportedIssue { get; init; }

    [Range(0, 999_999_999, ErrorMessage = "O desconto não pode ser negativo.")]
    public decimal DiscountAmount { get; init; }

    public DateTimeOffset? ScheduledAt { get; init; }
}

/// <summary>Written by the mechanic from the yard, which is why it is not part
/// of the front desk update.</summary>
public record UpdateDiagnosisRequest
{
    [StringLength(4000)]
    public string? Diagnosis { get; init; }
}

public record ChangeStatusRequest
{
    [Required]
    public ServiceOrderStatus ToStatus { get; init; }

    /// <summary>Free note kept in the history. Required when
    /// <see cref="WaiveApproval"/> is set, since D-13 only allows the waiver if
    /// the reason is recorded.</summary>
    [StringLength(1000)]
    public string? Note { get; init; }

    /// <summary>Admin starting the work without an approved quote (D-13).</summary>
    public bool WaiveApproval { get; init; }
}

/// <summary>Price and description are frozen on insert (D-07): what is sent
/// here is what the order shows forever, even if the part is renamed or
/// repriced later.</summary>
public record ServiceOrderItemRequest
{
    [Required]
    public ItemType ItemType { get; init; }

    /// <summary>Required for PART, must be null for SERVICE — the database
    /// enforces it through ck_item_part_required.</summary>
    public Guid? PartId { get; init; }

    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string Description { get; init; } = string.Empty;

    [Range(0.001, 9_999_999, ErrorMessage = "A quantidade deve ser maior que zero.")]
    public decimal Quantity { get; init; }

    [Range(0, 999_999_999, ErrorMessage = "O preço não pode ser negativo.")]
    public decimal UnitPrice { get; init; }
}

// ---------------------------------------------------------------------------
// Responses
// ---------------------------------------------------------------------------

/// <summary>What a Kanban card needs. Total is computed, never stored (D-09).</summary>
public record ServiceOrderSummary(
    Guid Id,
    int Number,
    ServiceOrderStatus Status,
    Guid VehicleId,
    string VehiclePlate,
    string VehicleDescription,
    Guid CustomerId,
    string CustomerName,
    Guid? MechanicId,
    string? MechanicName,
    decimal Total,
    bool HasApprovedQuote,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ScheduledAt,
    DateTimeOffset? ClosedAt);

public record ServiceOrderItemResponse(
    Guid Id,
    ItemType ItemType,
    Guid? PartId,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Total,
    DateTimeOffset CreatedAt);

public record ServiceOrderStatusChange(
    Guid Id,
    ServiceOrderStatus? FromStatus,
    ServiceOrderStatus ToStatus,
    string ChangedByName,
    string? Note,
    DateTimeOffset ChangedAt);

public record ServiceOrderDetail(
    Guid Id,
    int Number,
    ServiceOrderStatus Status,
    Guid VehicleId,
    string VehiclePlate,
    string VehicleDescription,
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    Guid? MechanicId,
    string? MechanicName,
    int? Mileage,
    string? ReportedIssue,
    string? Diagnosis,
    string? ApprovalWaivedNote,
    decimal ItemsTotal,
    decimal DiscountAmount,
    decimal Total,
    IReadOnlyList<ServiceOrderItemResponse> Items,
    IReadOnlyList<ServiceOrderStatusChange> History,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ScheduledAt,
    DateTimeOffset? ClosedAt);

public record AllowedTransition(ServiceOrderStatus ToStatus, bool RequiresNote);
