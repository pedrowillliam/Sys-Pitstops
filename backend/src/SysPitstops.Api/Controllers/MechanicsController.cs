using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SysPitstops.Api.Auth;
using SysPitstops.Api.Contracts;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;

namespace SysPitstops.Api.Controllers;

/// <summary>
/// The mechanics screen: who is busy, on which car, and what is waiting for
/// each of them. A read over users and service_orders — there is no mechanics
/// table (D-48), so a mechanic is a user with that role, and their workload
/// is whatever is assigned to them and not finished.
/// </summary>
[ApiController]
[Route("api/mechanics")]
[Authorize(Roles = Roles.Desk)]
public class MechanicsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MechanicWorkload>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MechanicWorkload>>> List(CancellationToken ct)
    {
        var workshopId = User.WorkshopId();

        var mechanics = await db.Users.AsNoTracking()
            .Where(u => u.WorkshopId == workshopId
                     && u.Role == UserRole.Mechanic
                     && u.IsActive)
            .OrderBy(u => u.Name)
            .Select(u => new { u.Id, u.Name })
            .ToListAsync(ct);

        // Every pending order with a mechanic, read once and split in memory:
        // a workshop has a handful of mechanics and a few dozen open orders,
        // and one query per card would cost more than the rows themselves.
        var pending = await db.ServiceOrders.AsNoTracking()
            .Where(o => o.WorkshopId == workshopId
                     && o.MechanicId != null
                     && MechanicQueue.Pending.Contains(o.Status))
            .Select(o => new
            {
                o.Id,
                o.Number,
                o.Status,
                o.MechanicId,
                o.OpenedAt,
                o.Vehicle.Plate,
                o.Vehicle.Brand,
                o.Vehicle.Model,
                o.Vehicle.ModelYear
            })
            .ToListAsync(ct);

        var workloads = mechanics.Select(mechanic =>
        {
            var theirs = pending
                .Where(o => o.MechanicId == mechanic.Id)
                .OrderBy(o => MechanicQueue.Rank(o.Status))
                .ThenBy(o => o.OpenedAt)
                .Select(o => new QueuedOrder(
                    o.Id,
                    o.Number,
                    o.Status,
                    o.Plate,
                    VehicleLabel.Describe(o.Brand, o.Model, o.ModelYear)))
                .ToList();

            // Hands-on work ranks first, so if the head of the list is not
            // hands-on, nothing is: the mechanic is free and everything queues.
            var current = theirs.FirstOrDefault(o => MechanicQueue.IsHandsOn(o.Status));
            var queue = current is null ? theirs : theirs.Skip(1).ToList();

            return new MechanicWorkload(
                mechanic.Id,
                mechanic.Name,
                current is not null,
                current,
                queue.FirstOrDefault(),
                queue.Count);
        }).ToList();

        return Ok(workloads);
    }
}
