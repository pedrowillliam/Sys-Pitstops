namespace SysPitstops.Api.Domain;

/// <summary>
/// What a mechanic has in hand, read from the orders assigned to them. The
/// mechanics screen draws four things from it — busy or free, the car under
/// the name, "Next:" and the size of the queue — and all four come from here,
/// so they cannot disagree with each other or with the board.
/// </summary>
public static class MechanicQueue
{
    /// <summary>
    /// Assigned and not finished. READY, DELIVERED and CANCELED leave the
    /// mechanic's hands; the rest stay with them even when the car has not
    /// arrived (REQUESTED and CONFIRMED may already carry a mechanic — D-38
    /// only makes one mandatory from IN_YARD on).
    /// </summary>
    public static readonly ServiceOrderStatus[] Pending =
    [
        ServiceOrderStatus.Requested,
        ServiceOrderStatus.Confirmed,
        ServiceOrderStatus.InYard,
        ServiceOrderStatus.AwaitingApproval,
        ServiceOrderStatus.InProgress
    ];

    /// <summary>The mechanic is physically on the car — diagnosing or
    /// executing. Anything else is waiting for someone: the customer, the
    /// front desk, or the car itself.</summary>
    public static bool IsHandsOn(ServiceOrderStatus status) =>
        status is ServiceOrderStatus.InYard or ServiceOrderStatus.InProgress;

    /// <summary>
    /// The order the queue is served in. Work already started comes first,
    /// then the car sitting in the yard, then the one whose customer has not
    /// answered, and last the one that has not arrived. Within a rank the
    /// oldest order goes first, which is the caller's tie-break.
    /// </summary>
    public static int Rank(ServiceOrderStatus status) => status switch
    {
        ServiceOrderStatus.InProgress => 0,
        ServiceOrderStatus.InYard => 1,
        ServiceOrderStatus.Confirmed => 2,
        ServiceOrderStatus.AwaitingApproval => 3,
        ServiceOrderStatus.Requested => 4,
        _ => int.MaxValue
    };
}
