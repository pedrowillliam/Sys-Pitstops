using System.Security.Claims;
using SysPitstops.Api.Domain;

namespace SysPitstops.Api.Auth;

// Reads the claims TokenService puts in the token. The workshop comes from the
// token rather than from a constant so that nothing has to be rewritten when
// D-02 is revisited — it is still always 1 in the MVP.
public static class CurrentUser
{
    public static Guid Id(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst(TokenService.SubjectClaim)?.Value, out var id)
            ? id
            : Guid.Empty;

    /// <summary>An unreadable role falls back to the least privileged one: the
    /// workflow then refuses the transition instead of allowing it by accident.</summary>
    public static UserRole Role(this ClaimsPrincipal principal) =>
        EnumExtensions.TryParseLabel(
            typeof(UserRole), principal.FindFirst(TokenService.RoleClaim)?.Value, out var role)
            ? (UserRole)role!
            : UserRole.Mechanic;

    public static int WorkshopId(this ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirst(TokenService.WorkshopClaim)?.Value, out var workshopId)
            ? workshopId
            : AdminSeeder.MvpWorkshopId;
}
