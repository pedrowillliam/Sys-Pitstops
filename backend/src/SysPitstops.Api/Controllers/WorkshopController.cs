using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SysPitstops.Api.Auth;
using SysPitstops.Api.Contracts;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;

namespace SysPitstops.Api.Controllers;

/// <summary>
/// The workshop's own registration data. Until this existed, the name was
/// whatever the seeder wrote and could only be changed in the database — and it
/// is not decorative: it is what every customer reads at the top of the public
/// quote link.
/// <para>
/// Reading is open to anyone signed in, because the name belongs in headers and
/// documents. Writing is the admin's.
/// </para>
/// </summary>
[ApiController]
[Route("api/workshop")]
[Authorize]
public class WorkshopController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(WorkshopResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkshopResponse>> Get(CancellationToken ct)
    {
        var workshop = await Current(ct);

        return workshop is null ? NotFound() : Ok(Describe(workshop));
    }

    [HttpPut]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(WorkshopResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkshopResponse>> Update(
        WorkshopRequest request, CancellationToken ct)
    {
        var workshop = await Current(ct);

        if (workshop is null)
        {
            return NotFound();
        }

        // The annotation measures what was sent, and the trim happens here — so
        // "  a  " passes MinimumLength with five characters and would be stored
        // as one. The name is what the customer reads at the top of the public
        // quote, so it is checked after trimming.
        var name = request.Name.Trim();

        if (name.Length < 2)
        {
            ModelState.AddModelError(
                nameof(request.Name), "O nome deve ter entre 2 e 120 caracteres.");
            return ValidationProblem(ModelState);
        }

        // Same rules as the customer's: digits only in the column, formatting is
        // a screen concern. Both fields are optional, and an empty one is null
        // rather than "", so "has no document" stays different from "has a blank
        // one".
        var document = Blank(request.Document);

        if (document is not null)
        {
            document = TaxDocument.Normalize(document);

            if (!TaxDocument.IsValid(document))
            {
                ModelState.AddModelError(nameof(request.Document), "CPF ou CNPJ inválido.");
                return ValidationProblem(ModelState);
            }
        }

        var phone = Blank(request.Phone);

        if (phone is not null)
        {
            phone = PhoneNumber.StripCountryCode(PhoneNumber.Normalize(phone));

            if (!PhoneNumber.IsValid(phone))
            {
                ModelState.AddModelError(
                    nameof(request.Phone), "Telefone inválido. Informe DDD e número.");
                return ValidationProblem(ModelState);
            }
        }

        workshop.Name = name;
        workshop.Document = document;
        workshop.Phone = phone;
        workshop.Address = Blank(request.Address);

        await db.SaveChangesAsync(ct);

        return Ok(Describe(workshop));
    }

    private Task<Workshop?> Current(CancellationToken ct) =>
        db.Workshops.SingleOrDefaultAsync(w => w.Id == User.WorkshopId(), ct);

    private static WorkshopResponse Describe(Workshop workshop) => new(
        workshop.Id,
        workshop.Name,
        workshop.Document,
        workshop.Phone,
        workshop.Address);

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
