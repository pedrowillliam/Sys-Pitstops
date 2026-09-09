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
[Route("api/customers")]
[Authorize]
public class CustomersController(AppDbContext db) : ControllerBase
{
    private static readonly Expression<Func<Customer, CustomerResponse>> ToResponse =
        c => new CustomerResponse(
            c.Id, c.Name, c.Phone, c.Document, c.Email, c.Notes, c.IsActive,
            c.Vehicles
                .OrderBy(v => v.Brand)
                .ThenBy(v => v.Model)
                .Select(v => new CustomerVehicleSummary(v.Id, v.Plate, v.Brand, v.Model, v.ModelYear))
                .ToList(),
            c.CreatedAt, c.UpdatedAt);

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CustomerResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CustomerResponse>>> List(
        [FromQuery] string? search,
        [FromQuery] bool includeInactive,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var (currentPage, size) = PagedResult<CustomerResponse>.Clamp(page, pageSize);
        var workshopId = User.WorkshopId();

        var query = db.Customers.AsNoTracking().Where(c => c.WorkshopId == workshopId);

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        var term = search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(term))
        {
            // The attendant types either a name or a phone. Three digits is
            // where a phone search stops matching every record by accident.
            var digits = PhoneNumber.Normalize(term);
            query = digits.Length >= 3
                ? query.Where(c => c.Name.ToLower().Contains(term) || c.Phone.Contains(digits))
                : query.Where(c => c.Name.ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.Name)
            .Skip((currentPage - 1) * size)
            .Take(size)
            .Select(ToResponse)
            .ToListAsync(ct);

        return Ok(new PagedResult<CustomerResponse>(items, currentPage, size, total));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> Get(Guid id, CancellationToken ct)
    {
        var customer = await Describe(id, ct);
        return customer is null ? CustomerNotFound() : Ok(customer);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Desk)]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomerResponse>> Create(
        CustomerRequest request, CancellationToken ct)
    {
        if (!TryNormalize(request, out var phone, out var document))
        {
            return ValidationProblem(ModelState);
        }

        var now = DateTimeOffset.UtcNow;
        var customer = new Customer
        {
            WorkshopId = User.WorkshopId(),
            Name = request.Name.Trim(),
            Phone = phone,
            Document = document,
            Email = Blank(request.Email)?.ToLowerInvariant(),
            Notes = Blank(request.Notes),
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);

        var response = new CustomerResponse(
            customer.Id, customer.Name, customer.Phone, customer.Document, customer.Email,
            customer.Notes, customer.IsActive, [], customer.CreatedAt, customer.UpdatedAt);

        return CreatedAtAction(nameof(Get), new { id = customer.Id }, response);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Desk)]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> Update(
        Guid id, CustomerRequest request, CancellationToken ct)
    {
        if (!TryNormalize(request, out var phone, out var document))
        {
            return ValidationProblem(ModelState);
        }

        var workshopId = User.WorkshopId();
        var customer = await db.Customers
            .SingleOrDefaultAsync(c => c.Id == id && c.WorkshopId == workshopId, ct);

        if (customer is null)
        {
            return CustomerNotFound();
        }

        customer.Name = request.Name.Trim();
        customer.Phone = phone;
        customer.Document = document;
        customer.Email = Blank(request.Email)?.ToLowerInvariant();
        customer.Notes = Blank(request.Notes);
        customer.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(await Describe(id, ct));
    }

    // A customer who already has service orders can never be removed — the
    // snapshot of D-08 points at this row. Deactivating takes them out of the
    // pickers and keeps the history readable.
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Desk)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Deactivate(Guid id, CancellationToken ct) =>
        SetActive(id, false, ct);

    [HttpPost("{id:guid}/reactivate")]
    [Authorize(Roles = Roles.Desk)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Reactivate(Guid id, CancellationToken ct) =>
        SetActive(id, true, ct);

    private async Task<IActionResult> SetActive(Guid id, bool isActive, CancellationToken ct)
    {
        var workshopId = User.WorkshopId();
        var customer = await db.Customers
            .SingleOrDefaultAsync(c => c.Id == id && c.WorkshopId == workshopId, ct);

        if (customer is null)
        {
            return CustomerNotFound();
        }

        if (customer.IsActive != isActive)
        {
            customer.IsActive = isActive;
            customer.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    private Task<CustomerResponse?> Describe(Guid id, CancellationToken ct)
    {
        var workshopId = User.WorkshopId();
        return db.Customers.AsNoTracking()
            .Where(c => c.Id == id && c.WorkshopId == workshopId)
            .Select(ToResponse)
            .SingleOrDefaultAsync(ct)!;
    }

    private bool TryNormalize(CustomerRequest request, out string phone, out string? document)
    {
        phone = PhoneNumber.StripCountryCode(PhoneNumber.Normalize(request.Phone));
        document = null;

        if (!PhoneNumber.IsValid(phone))
        {
            ModelState.AddModelError(
                nameof(request.Phone), "Telefone inválido. Informe DDD e número.");
        }

        var rawDocument = Blank(request.Document);
        if (rawDocument is not null)
        {
            document = TaxDocument.Normalize(rawDocument);
            if (!TaxDocument.IsValid(document))
            {
                ModelState.AddModelError(nameof(request.Document), "CPF ou CNPJ inválido.");
            }
        }

        return ModelState.IsValid;
    }

    private NotFoundObjectResult CustomerNotFound() => NotFound(new ProblemDetails
    {
        Status = StatusCodes.Status404NotFound,
        Title = "Cliente não encontrado."
    });

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
