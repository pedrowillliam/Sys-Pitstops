using NpgsqlTypes;

namespace SysPitstops.Api.Domain;

// PgName keeps the database labels uppercase, as documented in docs/schema.sql,
// while the C# members stay idiomatic.

public enum UserRole
{
    [PgName("ADMIN")] Admin,
    [PgName("ATTENDANT")] Attendant,
    [PgName("MECHANIC")] Mechanic
}

public enum ServiceOrderStatus
{
    [PgName("REQUESTED")] Requested,
    [PgName("CONFIRMED")] Confirmed,
    [PgName("IN_YARD")] InYard,
    [PgName("AWAITING_APPROVAL")] AwaitingApproval,
    [PgName("IN_PROGRESS")] InProgress,
    [PgName("READY")] Ready,
    [PgName("DELIVERED")] Delivered,
    [PgName("CANCELED")] Canceled
}

public enum ItemType
{
    [PgName("SERVICE")] Service,
    [PgName("PART")] Part
}

public enum QuoteStatus
{
    [PgName("SENT")] Sent,
    [PgName("APPROVED")] Approved,
    [PgName("REJECTED")] Rejected,
    [PgName("EXPIRED")] Expired
}

public enum MovementType
{
    [PgName("IN")] In,
    [PgName("OUT")] Out,
    [PgName("ADJUSTMENT")] Adjustment
}
