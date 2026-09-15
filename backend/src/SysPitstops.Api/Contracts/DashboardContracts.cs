using SysPitstops.Api.Domain;

namespace SysPitstops.Api.Contracts;

/// <summary>A number for the current month next to the same number a month
/// earlier. The variation is computed on the server so that every card applies
/// the same rule for "there was nothing to compare against" (D-47).</summary>
public record MonthlyMetric(decimal Current, decimal Previous, decimal? ChangePercent);

public record StatusCount(ServiceOrderStatus Status, int Count);

public record MonthlyRevenue(int Year, int Month, decimal Total);

/// <summary>Only what the model can answer: how many orders this mechanic
/// closed. D-47 cut the quality score and the specialty the prototype draws —
/// neither has any source.</summary>
public record MechanicProductivity(Guid Id, string Name, int ClosedOrders, decimal Revenue);

public record TopService(string Description, int Times, decimal Revenue);

public record RecentOrder(
    Guid Id,
    int Number,
    ServiceOrderStatus Status,
    string CustomerName,
    string VehicleDescription,
    string VehiclePlate,
    string? MechanicName,
    decimal Total,
    DateTimeOffset OpenedAt);

public record DashboardResponse(
    IReadOnlyList<StatusCount> ByStatus,
    MonthlyMetric Revenue,
    MonthlyMetric AverageTicket,
    MonthlyMetric CustomersServed,
    MonthlyMetric OpenedOrders,
    IReadOnlyList<MonthlyRevenue> RevenueHistory,
    IReadOnlyList<MechanicProductivity> Mechanics,
    IReadOnlyList<TopService> TopServices,
    /// <summary>Average hours between IN_PROGRESS and READY. Null while no order
    /// has gone through both — the KPI of §6 exists only because
    /// status_history does.</summary>
    double? AverageExecutionHours,
    int PartsBelowMinimum,
    IReadOnlyList<RecentOrder> Recent);
