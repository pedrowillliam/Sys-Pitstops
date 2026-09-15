using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SysPitstops.Api.Auth;
using SysPitstops.Api.Contracts;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;

namespace SysPitstops.Api.Controllers;

/// <summary>
/// The KPIs of §6 of data-model.md. None of them needs a table that does not
/// already exist, which is what that section set out to guarantee.
/// <para>
/// Revenue always reads the price frozen on the item (D-07). Joining parts to
/// read the current price would rewrite past revenue every time somebody
/// repriced a part — the report would change without a single order changing.
/// </para>
/// </summary>
[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = Roles.Admin)]
public class DashboardController(AppDbContext db) : ControllerBase
{
    /// <summary>Six buckets, the last of them the month still running.</summary>
    private const int HistoryMonths = 6;

    private const int RecentCount = 6;
    private const int RankedMechanics = 5;
    private const int RankedServices = 5;

    [HttpGet]
    [ProducesResponseType(typeof(DashboardResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardResponse>> Get(CancellationToken ct)
    {
        var workshopId = User.WorkshopId();
        var now = DateTimeOffset.UtcNow;

        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var previousStart = monthStart.AddMonths(-1);
        var windowStart = monthStart.AddMonths(-(HistoryMonths - 1));

        var closed = await ClosedOrders(workshopId, windowStart, ct);

        var thisMonth = closed.Where(o => o.ClosedAt >= monthStart).ToList();
        var lastMonth = closed
            .Where(o => o.ClosedAt >= previousStart && o.ClosedAt < monthStart)
            .ToList();

        return Ok(new DashboardResponse(
            await ByStatus(workshopId, ct),
            Metric(thisMonth.Sum(o => o.Total), lastMonth.Sum(o => o.Total)),
            Metric(AverageTicket(thisMonth), AverageTicket(lastMonth)),
            Metric(Served(thisMonth), Served(lastMonth)),
            await OpenedOrders(workshopId, monthStart, previousStart, ct),
            History(closed, windowStart),
            await Ranking(workshopId, thisMonth, ct),
            await TopServices(workshopId, monthStart, ct),
            await AverageExecutionHours(workshopId, windowStart, ct),
            await db.Parts.CountAsync(
                p => p.WorkshopId == workshopId
                  && p.IsActive
                  && p.QuantityOnHand <= p.MinQuantity, ct),
            await Recent(workshopId, ct)));
    }

    /// <summary>
    /// Every closed order in the window, with what it was worth. Read once and
    /// grouped in memory: a workshop closes tens of orders a month, and six
    /// separate aggregate queries over the same rows would cost more than the
    /// rows themselves.
    /// </summary>
    private async Task<List<ClosedOrder>> ClosedOrders(
        int workshopId, DateTimeOffset from, CancellationToken ct) =>
        await db.ServiceOrders.AsNoTracking()
            .Where(o => o.WorkshopId == workshopId
                     && o.ClosedAt != null
                     && o.ClosedAt >= from)
            .Select(o => new ClosedOrder(
                o.ClosedAt!.Value,
                o.CustomerId,
                o.MechanicId,
                // D-07 again: the sum walks the items, never the parts table.
                o.Items.Sum(i => i.Quantity * i.UnitPrice) - o.DiscountAmount))
            .ToListAsync(ct);

    private async Task<List<StatusCount>> ByStatus(int workshopId, CancellationToken ct)
    {
        var counted = await db.ServiceOrders.AsNoTracking()
            .Where(o => o.WorkshopId == workshopId)
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // Every status appears, including the ones at zero: a tile that vanishes
        // when it empties makes the board look like it lost a column.
        return Enum.GetValues<ServiceOrderStatus>()
            .Select(status => new StatusCount(
                status, counted.FirstOrDefault(c => c.Status == status)?.Count ?? 0))
            .ToList();
    }

    /// <summary>
    /// Orders <b>opened</b> in the month, not the ones open right now. The
    /// prototype labels this card "OS em Aberto" and still compares it with the
    /// previous month, which a point-in-time count cannot answer — and the yard
    /// tiles above it already say how much work is open today.
    /// </summary>
    private async Task<MonthlyMetric> OpenedOrders(
        int workshopId, DateTimeOffset monthStart, DateTimeOffset previousStart,
        CancellationToken ct)
    {
        var opened = db.ServiceOrders.AsNoTracking()
            .Where(o => o.WorkshopId == workshopId && o.OpenedAt >= previousStart);

        var current = await opened.CountAsync(o => o.OpenedAt >= monthStart, ct);
        var previous = await opened.CountAsync(o => o.OpenedAt < monthStart, ct);

        return Metric(current, previous);
    }

    private static List<MonthlyRevenue> History(
        List<ClosedOrder> closed, DateTimeOffset windowStart)
    {
        List<MonthlyRevenue> history = [];

        // Walking the months instead of grouping the rows so that a month with
        // nothing closed still draws a column at zero, rather than disappearing
        // and shifting the chart.
        for (var i = 0; i < HistoryMonths; i++)
        {
            var start = windowStart.AddMonths(i);
            var end = start.AddMonths(1);

            history.Add(new MonthlyRevenue(
                start.Year,
                start.Month,
                closed.Where(o => o.ClosedAt >= start && o.ClosedAt < end).Sum(o => o.Total)));
        }

        return history;
    }

    private async Task<List<MechanicProductivity>> Ranking(
        int workshopId, List<ClosedOrder> thisMonth, CancellationToken ct)
    {
        var names = await db.Users.AsNoTracking()
            .Where(u => u.WorkshopId == workshopId && u.Role == UserRole.Mechanic)
            .Select(u => new { u.Id, u.Name })
            .ToListAsync(ct);

        return names
            .Select(mechanic =>
            {
                var theirs = thisMonth.Where(o => o.MechanicId == mechanic.Id).ToList();

                return new MechanicProductivity(
                    mechanic.Id, mechanic.Name, theirs.Count, theirs.Sum(o => o.Total));
            })
            .OrderByDescending(m => m.ClosedOrders)
            .ThenByDescending(m => m.Revenue)
            .Take(RankedMechanics)
            .ToList();
    }

    /// <summary>Grouped by the frozen description (D-07), which is what the
    /// customer was charged for — not by a service catalogue, which does not
    /// exist.</summary>
    private async Task<List<TopService>> TopServices(
        int workshopId, DateTimeOffset from, CancellationToken ct)
    {
        // Grouped into an anonymous type and only then into the record: a group
        // projected straight into a constructor does not translate, which is why
        // ServiceOrdersController.Summarize already does the same two steps.
        var rows = await db.ServiceOrderItems.AsNoTracking()
            .Where(i => i.ItemType == ItemType.Service
                     && i.ServiceOrder.WorkshopId == workshopId
                     && i.ServiceOrder.ClosedAt != null
                     && i.ServiceOrder.ClosedAt >= from)
            .GroupBy(i => i.Description)
            .Select(g => new
            {
                Description = g.Key,
                Times = g.Count(),
                Revenue = g.Sum(i => i.Quantity * i.UnitPrice)
            })
            .OrderByDescending(s => s.Times)
            .Take(RankedServices)
            .ToListAsync(ct);

        return rows
            .Select(r => new TopService(r.Description, r.Times, r.Revenue))
            .ToList();
    }

    /// <summary>
    /// How long the work itself takes, measured between the two transitions that
    /// bracket it. This is the KPI that §6 says exists only because
    /// status_history exists: nothing else records when execution began.
    /// </summary>
    private async Task<double?> AverageExecutionHours(
        int workshopId, DateTimeOffset from, CancellationToken ct)
    {
        var marks = await db.ServiceOrderStatusHistory.AsNoTracking()
            .Where(h => h.ServiceOrder.WorkshopId == workshopId
                     && h.ChangedAt >= from
                     && (h.ToStatus == ServiceOrderStatus.InProgress
                      || h.ToStatus == ServiceOrderStatus.Ready))
            .Select(h => new { h.ServiceOrderId, h.ToStatus, h.ChangedAt })
            .ToListAsync(ct);

        var spans = marks
            .GroupBy(h => h.ServiceOrderId)
            .Select(g => new
            {
                Started = g.Where(h => h.ToStatus == ServiceOrderStatus.InProgress)
                    .Select(h => (DateTimeOffset?)h.ChangedAt).Min(),
                Finished = g.Where(h => h.ToStatus == ServiceOrderStatus.Ready)
                    .Select(h => (DateTimeOffset?)h.ChangedAt).Max()
            })
            // An order still in execution has no finish yet, and one that was
            // waived into READY may have no start. Neither is an average of zero.
            .Where(g => g.Started is not null && g.Finished is not null
                     && g.Finished > g.Started)
            .Select(g => (g.Finished!.Value - g.Started!.Value).TotalHours)
            .ToList();

        return spans.Count == 0 ? null : Math.Round(spans.Average(), 1);
    }

    private async Task<List<RecentOrder>> Recent(int workshopId, CancellationToken ct)
    {
        var rows = await db.ServiceOrders.AsNoTracking()
            .Where(o => o.WorkshopId == workshopId)
            .OrderByDescending(o => o.OpenedAt)
            .Take(RecentCount)
            .Select(o => new
            {
                o.Id,
                o.Number,
                o.Status,
                CustomerName = o.Customer.Name,
                o.Vehicle.Plate,
                o.Vehicle.Brand,
                o.Vehicle.Model,
                o.Vehicle.ModelYear,
                MechanicName = o.Mechanic != null ? o.Mechanic.Name : null,
                Total = o.Items.Sum(i => i.Quantity * i.UnitPrice) - o.DiscountAmount,
                o.OpenedAt
            })
            .ToListAsync(ct);

        return rows.Select(r => new RecentOrder(
            r.Id,
            r.Number,
            r.Status,
            r.CustomerName,
            VehicleLabel.Describe(r.Brand, r.Model, r.ModelYear),
            r.Plate,
            r.MechanicName,
            r.Total,
            r.OpenedAt)).ToList();
    }

    private static decimal AverageTicket(List<ClosedOrder> orders) =>
        orders.Count == 0 ? 0m : Math.Round(orders.Sum(o => o.Total) / orders.Count, 2);

    private static decimal Served(List<ClosedOrder> orders) =>
        orders.Select(o => o.CustomerId).Distinct().Count();

    private static MonthlyMetric Metric(decimal current, decimal previous) =>
        new(current, previous, PeriodChange.Percent(current, previous));

    private readonly record struct ClosedOrder(
        DateTimeOffset ClosedAt, Guid CustomerId, Guid? MechanicId, decimal Total);
}
