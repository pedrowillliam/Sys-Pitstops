using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SysPitstops.Api.Contracts;
using SysPitstops.Api.Controllers;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;
using Xunit;

namespace SysPitstops.Tests;

public class ServiceOrderOpeningTests
{
    [Fact]
    public async Task OpeningCopiesTheVehicleOwnerOntoTheOrder()
    {
        var db = TestApi.NewDatabase();
        var owner = db.AddCustomer("João Mendes");
        var vehicle = db.AddVehicle(owner, "GHJ4K56", "Fiat", "Strada", 2021);

        var created = TestApi.Body(await Controller(db).Open(
            new OpenServiceOrderRequest { VehicleId = vehicle.Id }, default));

        Assert.Equal(owner.Id, created.CustomerId);
        Assert.Equal("João Mendes", created.CustomerName);
        Assert.Equal("Fiat Strada 2021", created.VehicleDescription);
        Assert.Equal(ServiceOrderStatus.Requested, created.Status);
    }

    // D-08: the snapshot is the whole point. Selling the car must not rewrite
    // who was served, so the order keeps pointing at the previous owner.
    [Fact]
    public async Task SellingTheVehicleDoesNotRewriteTheOrder()
    {
        var db = TestApi.NewDatabase();
        var seller = db.AddCustomer("Dono antigo");
        var buyer = db.AddCustomer("Dono novo");
        var vehicle = db.AddVehicle(seller, "ABC1D23", "VW", "Gol");

        var order = TestApi.Body(await Controller(db).Open(
            new OpenServiceOrderRequest { VehicleId = vehicle.Id }, default));

        vehicle.OwnerId = buyer.Id;
        await db.SaveChangesAsync();

        var reread = TestApi.Body(await Controller(db).Get(order.Id, default));

        Assert.Equal(seller.Id, reread.CustomerId);
        Assert.Equal("Dono antigo", reread.CustomerName);
    }

    [Fact]
    public async Task OpeningRecordsTheFirstHistoryEntryWithNoOrigin()
    {
        var db = TestApi.NewDatabase();
        var vehicle = db.AddVehicle(db.AddCustomer(), "XYZ9A88", "Ford", "Ka");

        var order = TestApi.Body(await Controller(db).Open(
            new OpenServiceOrderRequest { VehicleId = vehicle.Id }, default));

        var entry = Assert.Single(order.History);
        Assert.Null(entry.FromStatus);
        Assert.Equal(ServiceOrderStatus.Requested, entry.ToStatus);
    }

    [Fact]
    public async Task ReportedIssueIsTrimmedAndBlankBecomesNull()
    {
        var db = TestApi.NewDatabase();
        var vehicle = db.AddVehicle(db.AddCustomer(), "AAA1A11", "Fiat", "Uno");
        var controller = Controller(db);

        var withIssue = TestApi.Body(await controller.Open(
            new OpenServiceOrderRequest { VehicleId = vehicle.Id, ReportedIssue = "  Barulho  " },
            default));
        var blank = TestApi.Body(await controller.Open(
            new OpenServiceOrderRequest { VehicleId = vehicle.Id, ReportedIssue = "   " }, default));

        Assert.Equal("Barulho", withIssue.ReportedIssue);
        Assert.Null(blank.ReportedIssue);
    }

    [Fact]
    public async Task AnUnknownVehicleIsRefused()
    {
        var db = TestApi.NewDatabase();

        var result = await Controller(db).Open(
            new OpenServiceOrderRequest { VehicleId = Guid.NewGuid() }, default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(db.ServiceOrders);
    }

    // The signed-in user must exist: history rows join to it, and the foreign
    // key guarantees that in production.
    private static ServiceOrdersController Controller(AppDbContext db) =>
        new ServiceOrdersController(db).AsUser(db.AddUser());
}

public class ServiceOrderTotalTests
{
    // D-09: the order has no total column. Every read recomputes it, so an
    // item added or removed can never leave a stale number behind.
    [Fact]
    public async Task TotalIsTheSumOfTheItemsMinusTheDiscount()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder(discount: 20m);
        db.AddItem(order, "Troca de óleo", quantity: 1.5m, unitPrice: 120.50m);
        db.AddItem(order, "Alinhamento", quantity: 1m, unitPrice: 90m);

        var detail = TestApi.Body(await Controller(db).Get(order.Id, default));

        Assert.Equal(270.75m, detail.ItemsTotal);   // 180.75 + 90
        Assert.Equal(20m, detail.DiscountAmount);
        Assert.Equal(250.75m, detail.Total);
    }

    [Fact]
    public async Task AnOrderWithoutItemsTotalsZero()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder();

        var detail = TestApi.Body(await Controller(db).Get(order.Id, default));

        Assert.Equal(0m, detail.Total);
        Assert.Empty(detail.Items);
    }

    [Fact]
    public async Task EachItemCarriesItsOwnTotal()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder();
        db.AddItem(order, "Pastilha", quantity: 2m, unitPrice: 89.90m);

        var detail = TestApi.Body(await Controller(db).Get(order.Id, default));

        Assert.Equal(179.80m, Assert.Single(detail.Items).Total);
    }

    // The signed-in user must exist: history rows join to it, and the foreign
    // key guarantees that in production.
    private static ServiceOrdersController Controller(AppDbContext db) =>
        new ServiceOrdersController(db).AsUser(db.AddUser());
}

public class ServiceOrderListTests
{
    [Fact]
    public async Task ListFiltersByStatus()
    {
        var db = TestApi.NewDatabase();
        db.AddServiceOrder(status: ServiceOrderStatus.InYard);
        db.AddServiceOrder(status: ServiceOrderStatus.Ready);
        db.AddServiceOrder(status: ServiceOrderStatus.InYard);

        var page = TestApi.Body(await Controller(db).List(
            ServiceOrderStatus.InYard, null, null, null, null, null, null, null, default));

        Assert.Equal(2, page.Total);
        Assert.All(page.Items, o => Assert.Equal(ServiceOrderStatus.InYard, o.Status));
    }

    [Fact]
    public async Task ListSeesOnlyItsOwnWorkshop()
    {
        var db = TestApi.NewDatabase();
        db.AddServiceOrder();
        var other = db.AddServiceOrder();
        other.WorkshopId = 99;
        await db.SaveChangesAsync();

        var page = TestApi.Body(await Controller(db).List(
            null, null, null, null, null, null, null, null, default));

        Assert.Equal(1, page.Total);
    }

    [Fact]
    public async Task TheQueueHoldsOnlyWhatIsStillToDo()
    {
        var db = TestApi.NewDatabase();
        var mechanic = db.AddUser("Roberto", UserRole.Mechanic);
        db.AddServiceOrder(status: ServiceOrderStatus.InProgress, mechanic: mechanic);
        db.AddServiceOrder(status: ServiceOrderStatus.Delivered, mechanic: mechanic);
        db.AddServiceOrder(status: ServiceOrderStatus.Canceled, mechanic: mechanic);
        db.AddServiceOrder(status: ServiceOrderStatus.InYard);   // outro mecânico

        var queue = TestApi.Body(
            await new ServiceOrdersController(db).AsUser(mechanic).MyQueue(default));

        var only = Assert.Single(queue);
        Assert.Equal(ServiceOrderStatus.InProgress, only.Status);
    }

    [Fact]
    public async Task AnUnknownOrderIsNotFound()
    {
        var db = TestApi.NewDatabase();

        var result = await Controller(db).Get(Guid.NewGuid(), default);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    // The signed-in user must exist: history rows join to it, and the foreign
    // key guarantees that in production.
    private static ServiceOrdersController Controller(AppDbContext db) =>
        new ServiceOrdersController(db).AsUser(db.AddUser());
}
