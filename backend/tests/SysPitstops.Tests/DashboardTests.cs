using Microsoft.EntityFrameworkCore;
using SysPitstops.Api.Controllers;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;
using Xunit;

namespace SysPitstops.Tests;

public class PeriodChangeTests
{
    [Theory]
    [InlineData(150, 100, 50)]
    [InlineData(80, 100, -20)]
    [InlineData(100, 100, 0)]
    public void TheVariationIsAPercentageOfThePreviousMonth(
        decimal current, decimal previous, decimal expected) =>
        Assert.Equal(expected, PeriodChange.Percent(current, previous));

    /// <summary>Growing from zero is a first month, not an infinite percentage.
    /// A card that printed a number here would be inventing a comparison.</summary>
    [Fact]
    public void ThereIsNoVariationAgainstZero()
    {
        Assert.Null(PeriodChange.Percent(1000m, 0m));
        Assert.Null(PeriodChange.Percent(0m, 0m));
    }

    [Fact]
    public void FallingToZeroIsStillAComparison() =>
        Assert.Equal(-100m, PeriodChange.Percent(0m, 500m));

    [Fact]
    public void TheVariationKeepsOneDecimal() =>
        Assert.Equal(14.3m, PeriodChange.Percent(1143m, 1000m));
}

public class DashboardControllerTests
{
    [Fact]
    public async Task TheRevenueSumsTheItemsMinusTheDiscount()
    {
        var db = TestApi.NewDatabase();
        var order = Closed(db, DateTimeOffset.UtcNow.AddDays(-1), discount: 50m);
        db.AddItem(order, "Revisão", 1m, 300m);
        db.AddItem(order, "Alinhamento", 2m, 100m);
        await db.SaveChangesAsync();

        var data = TestApi.Body(await Controller(db).Get(default));

        Assert.Equal(450m, data.Revenue.Current);
    }

    /// <summary>D-07: the price frozen on the item is what was charged. Repricing
    /// the part afterwards must not move a single number on the dashboard.</summary>
    [Fact]
    public async Task RepricingAPartDoesNotRewriteThePast()
    {
        var db = TestApi.NewDatabase();
        var part = db.AddPart("Filtro", salePrice: 50m);
        var order = Closed(db, DateTimeOffset.UtcNow.AddDays(-1));
        db.AddItem(order, "Filtro", quantity: 2m, unitPrice: 50m, part: part);
        await db.SaveChangesAsync();

        var before = TestApi.Body(await Controller(db).Get(default)).Revenue.Current;

        part.SalePrice = 999m;
        await db.SaveChangesAsync();

        var after = TestApi.Body(await Controller(db).Get(default)).Revenue.Current;

        Assert.Equal(100m, before);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task AnOrderStillOpenIsNotRevenue()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.InProgress);
        db.AddItem(order, "Em execução", 1m, 800m);
        await db.SaveChangesAsync();

        var data = TestApi.Body(await Controller(db).Get(default));

        Assert.Equal(0m, data.Revenue.Current);
    }

    [Fact]
    public async Task TheAverageTicketDividesByTheOrdersClosed()
    {
        var db = TestApi.NewDatabase();
        var yesterday = DateTimeOffset.UtcNow.AddDays(-1);

        db.AddItem(Closed(db, yesterday), "A", 1m, 300m);
        db.AddItem(Closed(db, yesterday), "B", 1m, 500m);
        await db.SaveChangesAsync();

        var data = TestApi.Body(await Controller(db).Get(default));

        Assert.Equal(800m, data.Revenue.Current);
        Assert.Equal(400m, data.AverageTicket.Current);
    }

    [Fact]
    public async Task TheSameCustomerTwiceIsCountedOnce()
    {
        var db = TestApi.NewDatabase();
        var customer = db.AddCustomer("Cliente Fiel");
        var yesterday = DateTimeOffset.UtcNow.AddDays(-1);

        Closed(db, yesterday, customer: customer);
        Closed(db, yesterday, customer: customer);
        Closed(db, yesterday);
        await db.SaveChangesAsync();

        var data = TestApi.Body(await Controller(db).Get(default));

        Assert.Equal(2m, data.CustomersServed.Current);
    }

    /// <summary>A month with nothing closed still draws a column: dropping it
    /// would shift the chart and hide the fact that the month was empty.</summary>
    [Fact]
    public async Task TheHistoryAlwaysHasSixMonths()
    {
        var db = TestApi.NewDatabase();

        var data = TestApi.Body(await Controller(db).Get(default));

        Assert.Equal(6, data.RevenueHistory.Count);
        Assert.All(data.RevenueHistory, month => Assert.Equal(0m, month.Total));
    }

    [Fact]
    public async Task TheHistoryPutsEachOrderInItsMonth()
    {
        var db = TestApi.NewDatabase();
        var now = DateTimeOffset.UtcNow;

        db.AddItem(Closed(db, now.AddDays(-1)), "Deste mês", 1m, 200m);
        db.AddItem(Closed(db, now.AddMonths(-2)), "De dois meses atrás", 1m, 700m);
        await db.SaveChangesAsync();

        var data = TestApi.Body(await Controller(db).Get(default));

        Assert.Equal(200m, data.RevenueHistory[^1].Total);
        Assert.Equal(700m, data.RevenueHistory[^3].Total);
    }

    [Fact]
    public async Task EveryStatusAppearsEvenAtZero()
    {
        var db = TestApi.NewDatabase();
        db.AddServiceOrder(status: ServiceOrderStatus.InYard);
        await db.SaveChangesAsync();

        var data = TestApi.Body(await Controller(db).Get(default));

        Assert.Equal(Enum.GetValues<ServiceOrderStatus>().Length, data.ByStatus.Count);
        Assert.Equal(1, data.ByStatus.Single(s => s.Status == ServiceOrderStatus.InYard).Count);
        Assert.Equal(0, data.ByStatus.Single(s => s.Status == ServiceOrderStatus.Ready).Count);
    }

    [Fact]
    public async Task TheRankingCountsWhatEachMechanicClosed()
    {
        var db = TestApi.NewDatabase();
        var busy = db.AddUser("Ocupado", UserRole.Mechanic);
        var quiet = db.AddUser("Tranquilo", UserRole.Mechanic);
        var yesterday = DateTimeOffset.UtcNow.AddDays(-1);

        db.AddItem(Closed(db, yesterday, mechanic: busy), "A", 1m, 100m);
        db.AddItem(Closed(db, yesterday, mechanic: busy), "B", 1m, 100m);
        db.AddItem(Closed(db, yesterday, mechanic: quiet), "C", 1m, 100m);
        await db.SaveChangesAsync();

        var data = TestApi.Body(await Controller(db).Get(default));

        Assert.Equal("Ocupado", data.Mechanics[0].Name);
        Assert.Equal(2, data.Mechanics[0].ClosedOrders);
        Assert.Equal(1, data.Mechanics[1].ClosedOrders);
    }

    /// <summary>§6: this KPI exists only because status_history exists. Without
    /// both marks there is nothing to average, and zero would be a lie.</summary>
    [Fact]
    public async Task TheExecutionTimeComesFromTheHistory()
    {
        var db = TestApi.NewDatabase();
        var order = Closed(db, DateTimeOffset.UtcNow.AddDays(-1));
        var started = DateTimeOffset.UtcNow.AddDays(-2);

        db.AddHistory(order, ServiceOrderStatus.InProgress, started);
        db.AddHistory(order, ServiceOrderStatus.Ready, started.AddHours(5));
        await db.SaveChangesAsync();

        var data = TestApi.Body(await Controller(db).Get(default));

        Assert.Equal(5.0, data.AverageExecutionHours);
    }

    [Fact]
    public async Task WithoutBothMarksThereIsNoAverage()
    {
        var db = TestApi.NewDatabase();
        var order = Closed(db, DateTimeOffset.UtcNow.AddDays(-1));
        db.AddHistory(order, ServiceOrderStatus.InProgress, DateTimeOffset.UtcNow.AddDays(-2));
        await db.SaveChangesAsync();

        var data = TestApi.Body(await Controller(db).Get(default));

        Assert.Null(data.AverageExecutionHours);
    }

    [Fact]
    public async Task ThePartsBelowTheMinimumAreCounted()
    {
        var db = TestApi.NewDatabase();
        db.AddPart("Em falta", quantityOnHand: 2m, minQuantity: 8m);
        db.AddPart("Sobrando", quantityOnHand: 40m, minQuantity: 8m);

        var data = TestApi.Body(await Controller(db).Get(default));

        Assert.Equal(1, data.PartsBelowMinimum);
    }

    private static DashboardController Controller(AppDbContext db) =>
        new DashboardController(db).AsUser();

    private static ServiceOrder Closed(
        AppDbContext db,
        DateTimeOffset closedAt,
        decimal discount = 0m,
        User? mechanic = null,
        Customer? customer = null)
    {
        var order = db.AddServiceOrder(
            status: ServiceOrderStatus.Delivered, mechanic: mechanic, discount: discount);

        order.ClosedAt = closedAt;

        if (customer is not null)
        {
            order.CustomerId = customer.Id;
        }

        db.SaveChanges();

        return order;
    }
}
