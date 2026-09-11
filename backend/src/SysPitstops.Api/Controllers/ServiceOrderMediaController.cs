using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SysPitstops.Api.Auth;
using SysPitstops.Api.Contracts;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;
using SysPitstops.Api.Storage;

namespace SysPitstops.Api.Controllers;

/// <summary>
/// The photographic record of the laudo. Who may write follows the diagnosis:
/// the front desk, or the mechanic responsible for this order — the photo is
/// part of the same report and should not have a looser rule than the text.
/// </summary>
[ApiController]
[Route("api/service-orders/{orderId:guid}/media")]
[Authorize]
public class ServiceOrderMediaController(AppDbContext db, IMediaStorage storage) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ServiceOrderMediaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ServiceOrderMediaResponse>>> List(
        Guid orderId, CancellationToken ct)
    {
        if (await FindOrder(orderId, ct) is null)
        {
            return OrderNotFound();
        }

        var items = await db.ServiceOrderMedia.AsNoTracking()
            .Where(m => m.ServiceOrderId == orderId)
            .OrderBy(m => m.UploadedAt)
            .Select(m => new ServiceOrderMediaResponse(
                m.Id,
                m.ServiceOrderId,
                m.ContentType,
                m.SizeBytes,
                m.Caption,
                m.UploadedByUser.Name,
                m.UploadedAt))
            .ToListAsync(ct);

        return Ok(items);
    }

    /// <summary>
    /// The bytes. They go through the API instead of handing out the storage
    /// address, because the destination is an implementation detail (D-25) and
    /// the bucket of D-26 is private — a direct link would either leak or stop
    /// working. The cookie of D-20 rides along on the img request by itself,
    /// since front and API share an origin (D-26).
    /// </summary>
    [HttpGet("{mediaId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Content(Guid orderId, Guid mediaId, CancellationToken ct)
    {
        var media = await Find(orderId, mediaId, ct);

        if (media is null)
        {
            return MediaNotFound();
        }

        var content = await storage.OpenAsync(media.StorageKey, ct);

        if (content is null)
        {
            return MediaNotFound();
        }

        return File(content, media.ContentType);
    }

    [HttpPost]
    [RequestSizeLimit(MediaRules.MaxBytes + 4096)]
    [ProducesResponseType(typeof(ServiceOrderMediaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ServiceOrderMediaResponse>> Upload(
        Guid orderId, [FromForm] UploadMediaRequest request, CancellationToken ct)
    {
        var order = await FindOrder(orderId, ct);

        if (order is null)
        {
            return OrderNotFound();
        }

        if (!MayWrite(order))
        {
            return Forbid();
        }

        if (ServiceOrderWorkflow.IsFinal(order.Status))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = $"A ordem de serviço está em {order.Status.ToPgName()} "
                    + "e não recebe mais fotos."
            });
        }

        var file = request.File;

        if (file.Length == 0)
        {
            ModelState.AddModelError(nameof(request.File), "O arquivo está vazio.");
            return ValidationProblem(ModelState);
        }

        if (file.Length > MediaRules.MaxBytes)
        {
            ModelState.AddModelError(
                nameof(request.File),
                $"A foto passa de {MediaRules.MaxBytes / (1024 * 1024)} MB.");
            return ValidationProblem(ModelState);
        }

        if (!MediaRules.IsAllowedType(file.ContentType))
        {
            ModelState.AddModelError(
                nameof(request.File),
                $"Formato não aceito. Envie {string.Join(", ", MediaRules.AllowedTypes)}.");
            return ValidationProblem(ModelState);
        }

        var contentType = MediaRules.Normalize(file.ContentType);

        await using var upload = file.OpenReadStream();
        var storageKey = await storage.SaveAsync(upload, contentType, ct);

        var media = new ServiceOrderMedia
        {
            ServiceOrderId = order.Id,
            StorageKey = storageKey,
            ContentType = contentType,
            SizeBytes = file.Length,
            Caption = string.IsNullOrWhiteSpace(request.Caption) ? null : request.Caption.Trim(),
            UploadedBy = User.Id(),
            UploadedAt = DateTimeOffset.UtcNow
        };

        db.ServiceOrderMedia.Add(media);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // The row is what makes the file reachable. Without it the bytes are
            // unreferenced garbage, so they go with the failure.
            await storage.DeleteAsync(storageKey, CancellationToken.None);
            throw;
        }

        await db.Entry(media).Reference(m => m.UploadedByUser).LoadAsync(ct);

        return CreatedAtAction(
            nameof(Content),
            new { orderId, mediaId = media.Id },
            Describe(media));
    }

    [HttpDelete("{mediaId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(Guid orderId, Guid mediaId, CancellationToken ct)
    {
        var order = await FindOrder(orderId, ct);

        if (order is null)
        {
            return OrderNotFound();
        }

        if (!MayWrite(order))
        {
            return Forbid();
        }

        var media = await Find(orderId, mediaId, ct);

        if (media is null)
        {
            return MediaNotFound();
        }

        db.ServiceOrderMedia.Remove(media);
        await db.SaveChangesAsync(ct);

        // Only after the row is gone: a file left behind is recoverable garbage,
        // while a row pointing at nothing is a broken thumbnail on the screen.
        await storage.DeleteAsync(media.StorageKey, ct);

        return NoContent();
    }

    /// <summary>The same rule as the diagnosis: the photo is part of the report,
    /// and a looser rule here would let any mechanic write into a laudo that is
    /// not theirs.</summary>
    private bool MayWrite(ServiceOrder order)
    {
        var role = User.Role();

        return role is UserRole.Admin or UserRole.Attendant
            || (role == UserRole.Mechanic && order.MechanicId == User.Id());
    }

    private Task<ServiceOrder?> FindOrder(Guid orderId, CancellationToken ct)
    {
        var workshopId = User.WorkshopId();

        return db.ServiceOrders
            .SingleOrDefaultAsync(o => o.Id == orderId && o.WorkshopId == workshopId, ct);
    }

    private Task<ServiceOrderMedia?> Find(Guid orderId, Guid mediaId, CancellationToken ct)
    {
        var workshopId = User.WorkshopId();

        return db.ServiceOrderMedia
            .SingleOrDefaultAsync(
                m => m.Id == mediaId
                  && m.ServiceOrderId == orderId
                  && m.ServiceOrder.WorkshopId == workshopId, ct);
    }

    private static ServiceOrderMediaResponse Describe(ServiceOrderMedia media) => new(
        media.Id,
        media.ServiceOrderId,
        media.ContentType,
        media.SizeBytes,
        media.Caption,
        media.UploadedByUser?.Name ?? string.Empty,
        media.UploadedAt);

    private NotFoundObjectResult OrderNotFound() => NotFound(new ProblemDetails
    {
        Status = StatusCodes.Status404NotFound,
        Title = "Ordem de serviço não encontrada."
    });

    private NotFoundObjectResult MediaNotFound() => NotFound(new ProblemDetails
    {
        Status = StatusCodes.Status404NotFound,
        Title = "Foto não encontrada."
    });
}
