namespace SysPitstops.Api.Auth;

// The strings match the user_role labels, which is what TokenService writes
// into the role claim.
public static class Roles
{
    public const string Admin = "ADMIN";
    public const string Attendant = "ATTENDANT";
    public const string Mechanic = "MECHANIC";

    // Front desk: who registers customers, vehicles and service orders.
    public const string Desk = $"{Admin},{Attendant}";
}
