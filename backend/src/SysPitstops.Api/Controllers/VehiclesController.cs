using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SysPitstops.Api.Auth;
using SysPitstops.Api.Contracts;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;

namespace SysPitstops.Api.Controllers;

[ApiController]
[Route("api/vehicles")]
[Authorize]
public class VehiclesController(AppDbContext db) : ControllerBase
{
    private static readonly Expression<Func<Vehicle, VehicleResponse>> ToResponse =
        v => new VehicleResponse(
            v.Id, v.OwnerId, v.Owner.Name, v.Plate, v.Brand, v.Model,
            v.ModelYear, v.Color, v.Vin, v.CreatedAt, v.UpdatedAt);

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<VehicleResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<VehicleResponse>>> List(
        [FromQuery] string? search,
        [FromQuery] Guid? ownerId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var (currentPage, size) = PagedResult<VehicleResponse>.Clamp(page, pageSize);
        var workshopId = User.WorkshopId();

        var query = db.Vehicles.AsNoTracking().Where(v => v.WorkshopId == workshopId);

        if (ownerId is not null)
        {
            query = query.Where(v => v.OwnerId == ownerId);
        }

        var term = search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(term))
        {
            // A plate is typed with or without the dash, so it is matched in
            // its normalized form; brand and model are matched as typed.
            var plate = PlateNumber.Normalize(term);
            query = query.Where(v =>
                v.Plate.Contains(plate)
                || v.Brand.ToLower().Contains(term)
                || v.Model.ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(v => v.Plate)
            .Skip((currentPage - 1) * size)
            .Take(size)
            .Select(ToResponse)
            .ToListAsync(ct);

        return Ok(new PagedResult<VehicleResponse>(items, currentPage, size, total));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(VehicleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VehicleResponse>> Get(Guid id, CancellationToken ct)
    {
        var vehicle = await Describe(id, ct);
        return vehicle is null ? VehicleNotFound() : Ok(vehicle);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Desk)]
    [ProducesResponseType(typeof(VehicleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VehicleResponse>> Create(
        VehicleRequest request, CancellationToken ct)
    {
        var normalized = await Validate(request, null, ct);
        if (normalized is null)
        {
            return ModelState.IsValid ? PlateTaken() : ValidationProblem(ModelState);
        }

        var now = DateTimeOffset.UtcNow;
        var vehicle = new Vehicle
        {
            WorkshopId = User.WorkshopId(),
            OwnerId = request.OwnerId,
            Plate = normalized.Value.Plate,
            Brand = request.Brand.Trim(),
            Model = request.Model.Trim(),
            ModelYear = request.ModelYear,
            Color = Blank(request.Color),
            Vin = normalized.Value.Vin,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = vehicle.Id }, await Describe(vehicle.Id, ct));
    }

    // Changing the owner is a sale, not a correction: D-08 keeps the previous
    // owner on every past service order, so the history does not move with it.
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Desk)]
    [ProducesResponseType(typeof(VehicleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VehicleResponse>> Update(
        Guid id, VehicleRequest request, CancellationToken ct)
    {
        var workshopId = User.WorkshopId();
        var vehicle = await db.Vehicles
            .SingleOrDefaultAsync(v => v.Id == id && v.WorkshopId == workshopId, ct);

        if (vehicle is null)
        {
            return VehicleNotFound();
        }

        var normalized = await Validate(request, id, ct);
        if (normalized is null)
        {
            return ModelState.IsValid ? PlateTaken() : ValidationProblem(ModelState);
        }

        vehicle.OwnerId = request.OwnerId;
        vehicle.Plate = normalized.Value.Plate;
        vehicle.Brand = request.Brand.Trim();
        vehicle.Model = request.Model.Trim();
        vehicle.ModelYear = request.ModelYear;
        vehicle.Color = Blank(request.Color);
        vehicle.Vin = normalized.Value.Vin;
        vehicle.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(await Describe(id, ct));
    }

    // There is no is_active on vehicles, so removal is real. It is refused as
    // soon as a service order references the row, which is what the FK would
    // do anyway — only with a message the screen can show.
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Desk)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var workshopId = User.WorkshopId();
        var vehicle = await db.Vehicles
            .SingleOrDefaultAsync(v => v.Id == id && v.WorkshopId == workshopId, ct);

        if (vehicle is null)
        {
            return VehicleNotFound();
        }

        if (await db.ServiceOrders.AnyAsync(o => o.VehicleId == id, ct))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "O veículo tem ordens de serviço e não pode ser excluído."
            });
        }

        db.Vehicles.Remove(vehicle);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    // Returns null on any rejection. ModelState says which: still valid means
    // the plate is taken, otherwise the fields themselves are wrong.
    private async Task<(string Plate, string? Vin)?> Validate(
        VehicleRequest request, Guid? currentId, CancellationToken ct)
    {
        var workshopId = User.WorkshopId();

        var plate = PlateNumber.Normalize(request.Plate);
        if (!PlateNumber.IsValid(plate))
        {
            ModelState.AddModelError(
                nameof(request.Plate), "Placa inválida. Use ABC1234 ou ABC1D23.");
        }

        string? vin = null;
        var rawVin = Blank(request.Vin);
        if (rawVin is not null)
        {
            vin = VehicleIdentificationNumber.Normalize(rawVin);
            if (!VehicleIdentificationNumber.IsValid(vin))
            {
                ModelState.AddModelError(
                    nameof(request.Vin), "Chassi inválido. São 17 caracteres, sem I, O ou Q.");
            }
        }

        if (request.ModelYear is { } year && (year < 1900 || year > DateTime.UtcNow.Year + 1))
        {
            ModelState.AddModelError(nameof(request.ModelYear), "Ano do modelo inválido.");
        }

        var ownerExists = await db.Customers.AnyAsync(
            c => c.Id == request.OwnerId && c.WorkshopId == workshopId && c.IsActive, ct);

        if (!ownerExists)
        {
            ModelState.AddModelError(nameof(request.OwnerId), "Cliente não encontrado ou inativo.");
        }

        if (!ModelState.IsValid)
        {
            return null;
        }

        // uq_vehicles_plate would raise this at the database, but a 409 with a
        // message is what the screen can actually show the attendant.
        var duplicates = db.Vehicles.Where(v => v.WorkshopId == workshopId && v.Plate == plate);
        if (currentId is { } editing)
        {
            duplicates = duplicates.Where(v => v.Id != editing);
        }

        return await duplicates.AnyAsync(ct) ? null : (plate, vin);
    }

    private Task<VehicleResponse?> Describe(Guid id, CancellationToken ct)
    {
        var workshopId = User.WorkshopId();
        return db.Vehicles.AsNoTracking()
            .Where(v => v.Id == id && v.WorkshopId == workshopId)
            .Select(ToResponse)
            .SingleOrDefaultAsync(ct)!;
    }

    private NotFoundObjectResult VehicleNotFound() => NotFound(new ProblemDetails
    {
        Status = StatusCodes.Status404NotFound,
        Title = "Veículo não encontrado."
    });

    private ConflictObjectResult PlateTaken() => Conflict(new ProblemDetails
    {
        Status = StatusCodes.Status409Conflict,
        Title = "Já existe um veículo com essa placa."
    });

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
