using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SysPitstops.Api.Auth;
using SysPitstops.Api.Contracts;
using SysPitstops.Api.Domain;

namespace SysPitstops.Api.Controllers;

/// <summary>
/// The routes and DTOs are published before the logic exists, so the front can
/// generate its TypeScript client and start the board on day one (D-18). Every
/// action answers 501 until its own pull request fills it in.
/// </summary>
[ApiController]
[Route("api/service-orders")]
[Authorize]
public class ServiceOrdersController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ServiceOrderSummary>), StatusCodes.Status200OK)]
    public ActionResult<PagedResult<ServiceOrderSummary>> List(
        [FromQuery] ServiceOrderStatus? status,
        [FromQuery] Guid? mechanicId,
        [FromQuery] Guid? vehicleId,
        [FromQuery] Guid? customerId,
        [FromQuery] DateTimeOffset? openedFrom,
        [FromQuery] DateTimeOffset? openedTo,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct) => Pending();

    /// <summary>The orders assigned to whoever is signed in — the mechanic's
    /// screen in the yard.</summary>
    [HttpGet("my-queue")]
    [ProducesResponseType(typeof(IReadOnlyList<ServiceOrderSummary>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ServiceOrderSummary>> MyQueue(CancellationToken ct) => Pending();

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ServiceOrderDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ServiceOrderDetail> Get(Guid id, CancellationToken ct) => Pending();

    /// <summary>Which moves the current user may make from here (D-13 and the
    /// permission table of data-model.md, section 5).</summary>
    [HttpGet("{id:guid}/allowed-transitions")]
    [ProducesResponseType(typeof(IReadOnlyList<AllowedTransition>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<IReadOnlyList<AllowedTransition>> AllowedTransitions(
        Guid id, CancellationToken ct) => Pending();

    [HttpPost]
    [Authorize(Roles = Roles.Desk)]
    [ProducesResponseType(typeof(ServiceOrderDetail), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<ServiceOrderDetail> Open(
        OpenServiceOrderRequest request, CancellationToken ct) => Pending();

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Desk)]
    [ProducesResponseType(typeof(ServiceOrderDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ServiceOrderDetail> Update(
        Guid id, UpdateServiceOrderRequest request, CancellationToken ct) => Pending();

    /// <summary>Separate from the update above because the diagnosis is the
    /// mechanic's, written from the yard, while the rest is the front desk's.</summary>
    [HttpPut("{id:guid}/diagnosis")]
    [ProducesResponseType(typeof(ServiceOrderDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ServiceOrderDetail> UpdateDiagnosis(
        Guid id, UpdateDiagnosisRequest request, CancellationToken ct) => Pending();

    /// <summary>Runs the transition through ServiceOrderWorkflow and records the
    /// move in service_order_status_history. A refused transition answers 409
    /// with the reason the workflow gave.</summary>
    [HttpPost("{id:guid}/status")]
    [ProducesResponseType(typeof(ServiceOrderDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public ActionResult<ServiceOrderDetail> ChangeStatus(
        Guid id, ChangeStatusRequest request, CancellationToken ct) => Pending();

    [HttpPost("{id:guid}/items")]
    [Authorize(Roles = Roles.Desk)]
    [ProducesResponseType(typeof(ServiceOrderItemResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ServiceOrderItemResponse> AddItem(
        Guid id, ServiceOrderItemRequest request, CancellationToken ct) => Pending();

    [HttpPut("{id:guid}/items/{itemId:guid}")]
    [Authorize(Roles = Roles.Desk)]
    [ProducesResponseType(typeof(ServiceOrderItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ServiceOrderItemResponse> UpdateItem(
        Guid id, Guid itemId, ServiceOrderItemRequest request, CancellationToken ct) => Pending();

    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    [Authorize(Roles = Roles.Desk)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DeleteItem(Guid id, Guid itemId, CancellationToken ct) => Pending();

    // A 501 cannot be mistaken for a working endpoint, which fake data could.
    // The front generates its types from the schema above and mocks locally
    // until each action lands.
    private ObjectResult Pending() =>
        new(new ProblemDetails
        {
            Status = StatusCodes.Status501NotImplemented,
            Title = "Endpoint ainda não implementado.",
            Detail = "O contrato está publicado para gerar o cliente TypeScript (D-18); "
                + "a lógica entra nos próximos PRs da Semana 2."
        })
        {
            StatusCode = StatusCodes.Status501NotImplemented
        };
}
