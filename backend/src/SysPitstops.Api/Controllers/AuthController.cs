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
