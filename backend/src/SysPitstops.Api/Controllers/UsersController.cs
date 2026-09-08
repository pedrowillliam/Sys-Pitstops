using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SysPitstops.Api.Auth;
using SysPitstops.Api.Contracts;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;

namespace SysPitstops.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = Roles.Desk)]
public class UsersController(AppDbContext db, IPasswordHasher hasher) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> List(
        [FromQuery] UserRole? role,
        [FromQuery] bool includeInactive,
        CancellationToken ct)
    {
        var workshopId = User.WorkshopId();
        var query = db.Users.AsNoTracking().Where(u => u.WorkshopId == workshopId);

        if (role is not null)
        {
            query = query.Where(u => u.Role == role);
        }

        if (!includeInactive)
        {
            query = query.Where(u => u.IsActive);
        }

        var users = await query
            .OrderBy(u => u.Name)
            .Select(u => new UserResponse(u.Id, u.Name, u.Email, u.Role, u.IsActive, u.CreatedAt))
            .ToListAsync(ct);

        return Ok(users);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Create(
        CreateUserRequest request, CancellationToken ct)
    {
        var workshopId = User.WorkshopId();
        var email = request.Email.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(u => u.WorkshopId == workshopId && u.Email == email, ct))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Já existe um usuário com este e-mail."
            });
        }

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            WorkshopId = workshopId,
            Name = request.Name.Trim(),
            Email = email,
            PasswordHash = hasher.Hash(request.Password),
            Role = request.Role,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        var response = new UserResponse(
            user.Id, user.Name, user.Email, user.Role, user.IsActive, user.CreatedAt);

        return CreatedAtAction(nameof(List), new { }, response);
    }
}
