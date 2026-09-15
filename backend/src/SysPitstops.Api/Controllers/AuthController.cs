using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SysPitstops.Api.Auth;
using SysPitstops.Api.Contracts;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;

namespace SysPitstops.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    AppDbContext db,
    IPasswordHasher hasher,
    TokenService tokens,
    IWebHostEnvironment env) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthenticatedUser), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthenticatedUser>> Login(
        LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);

        if (user is null || !user.IsActive || !hasher.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Credenciais inválidas."
            });
        }

        var (token, expiresAt) = tokens.Create(user);
        Response.Cookies.Append(AuthCookie.Name, token, AuthCookie.Build(env, expiresAt));

        return Ok(Describe(user));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(AuthCookie.Name, AuthCookie.Build(env, null));
        return NoContent();
    }

    /// <summary>
    /// Changing one's own password. The README tells whoever deploys to change
    /// the seeded admin password on first access (D-22), and until this existed
    /// there was no way to — the instruction pointed at nothing.
    /// <para>
    /// The current password is asked for even though the cookie already proves a
    /// session: the cookie says the browser signed in once, not that the person
    /// typing now is the owner of the account.
    /// </para>
    /// </summary>
    [HttpPost("password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == User.Id(), ct);

        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }

        if (!hasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            ModelState.AddModelError(
                nameof(request.CurrentPassword), "Senha atual incorreta.");
            return ValidationProblem(ModelState);
        }

        if (request.NewPassword == request.CurrentPassword)
        {
            ModelState.AddModelError(
                nameof(request.NewPassword), "A nova senha é igual à atual.");
            return ValidationProblem(ModelState);
        }

        user.PasswordHash = hasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        // The cookie of D-20 stays valid: it carries no password, and the token
        // has no refresh to revoke (D-19). Signing the person out here would
        // punish them for doing the right thing.
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(AuthenticatedUser), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthenticatedUser>> Me(CancellationToken ct)
    {
        var subject = User.FindFirst(TokenService.SubjectClaim)?.Value;
        if (!Guid.TryParse(subject, out var userId))
        {
            return Unauthorized();
        }
        
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId && u.IsActive, ct);
        return user is null ? Unauthorized() : Ok(Describe(user));
    }

    private static AuthenticatedUser Describe(User user) =>
        new(user.Id, user.Name, user.Email, user.Role.ToPgName(), user.WorkshopId);
}
