using System.Security.Claims;

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

    public static int WorkshopId(this ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirst(TokenService.WorkshopClaim)?.Value, out var workshopId)
            ? workshopId
            : AdminSeeder.MvpWorkshopId;
}
