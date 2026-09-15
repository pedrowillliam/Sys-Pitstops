using SysPitstops.Api.Domain;

namespace SysPitstops.Api.Contracts;

/// <summary>An order as the mechanics screen names it: number, plate and the
/// car. Enough to say "Marcos Santos – Fiat Uno"; the detail is one click away.</summary>
public record QueuedOrder(
    Guid Id,
    int Number,
    ServiceOrderStatus Status,
    string VehiclePlate,
    string VehicleDescription);

/// <summary>
/// One card of the mechanics screen. <see cref="Current"/> is the car the
/// mechanic is on right now (null when free), <see cref="Next"/> the first of
/// the queue, and <see cref="QueueSize"/> everything still assigned to them
/// beyond the current one. The rules are in <see cref="MechanicQueue"/>.
/// </summary>
public record MechanicWorkload(
    Guid Id,
    string Name,
    bool IsBusy,
    QueuedOrder? Current,
    QueuedOrder? Next,
    int QueueSize);
