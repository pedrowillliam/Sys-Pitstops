using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SysPitstops.Api.Contracts;
using SysPitstops.Api.Controllers;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;
using Xunit;

namespace SysPitstops.Tests;

public class ServiceOrderItemFreezingTests
{
    // D-07 is the whole reason the columns exist. If the item followed the part,
    // repricing a brake pad would rewrite last month's revenue.
    [Fact]
    public async Task RepricingThePartDoesNotChangeTheItemAlreadyOnTheOrder()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder();
        var part = db.AddPart("Pastilha de Freio Dianteira", salePrice: 89.90m);
        var controller = Controller(db);

        await controller.AddItem(order.Id, new ServiceOrderItemRequest
        {
            ItemType = ItemType.Part,
            PartId = part.Id,
            Description = "Pastilha de Freio Dianteira",
            Quantity = 1,
            UnitPrice = 89.90m
        }, default);

        part.SalePrice = 149.90m;
        part.Name = "Pastilha de Freio Dianteira (nova embalagem)";
        await db.SaveChangesAsync();

        var detail = TestApi.Body(await controller.Get(order.Id, default));
        var item = Assert.Single(detail.Items);

        Assert.Equal(89.90m, item.UnitPrice);
        Assert.Equal("Pastilha de Freio Dianteira", item.Description);
        Assert.Equal(89.90m, detail.Total);
    }

    [Fact]
    public async Task AnItemKeepsThePartIdForTraceability()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder();
        var part = db.AddPart("Disco de Freio");

        var created = TestApi.Body(await Controller(db).AddItem(order.Id, new ServiceOrderItemRequest
        {
            ItemType = ItemType.Part,
            PartId = part.Id,
            Description = "Disco de Freio (par)",
            Quantity = 1,
            UnitPrice = 240m
        }, default));

        Assert.Equal(part.Id, created.PartId);
        Assert.Equal(240m, created.Total);
    }

    private static ServiceOrdersController Controller(AppDbContext db) =>
        new ServiceOrdersController(db).AsUser(db.AddUser());
}

public class ServiceOrderItemValidationTests
{
    [Fact]
    public async Task APartItemWithoutAPartIsRefused()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder();

        var result = await Controller(db).AddItem(order.Id, new ServiceOrderItemRequest
        {
            ItemType = ItemType.Part,
            Description = "Peça sem identificação",
            Quantity = 1,
            UnitPrice = 10m
        }, default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(db.ServiceOrderItems);
    }

    [Fact]
    public async Task AServiceItemPointingAtAPartIsRefused()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder();
        var part = db.AddPart("Óleo");

        var result = await Controller(db).AddItem(order.Id, new ServiceOrderItemRequest
        {
            ItemType = ItemType.Service,
            PartId = part.Id,
            Description = "Troca de óleo",
            Quantity = 1,
            UnitPrice = 60m
        }, default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task AnUnknownPartIsRefused()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder();

        var result = await Controller(db).AddItem(order.Id, new ServiceOrderItemRequest
        {
            ItemType = ItemType.Part,
            PartId = Guid.NewGuid(),
            Description = "Fantasma",
            Quantity = 1,
            UnitPrice = 10m
        }, default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task AddingToAnUnknownOrderIsNotFound()
    {
        var db = TestApi.NewDatabase();

        var result = await Controller(db).AddItem(Guid.NewGuid(), Service(), default);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    private static ServiceOrderItemRequest Service() => new()
    {
        ItemType = ItemType.Service,
        Description = "Alinhamento",
        Quantity = 1,
        UnitPrice = 90m
    };

    private static ServiceOrdersController Controller(AppDbContext db) =>
        new ServiceOrdersController(db).AsUser(db.AddUser());
}

public class ServiceOrderItemEditingTests
{
    [Fact]
    public async Task AnItemCanBeCorrectedAndRemovedWhileTheWorkIsOpen()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.InProgress);
        var controller = Controller(db);

        var created = TestApi.Body(await controller.AddItem(order.Id, new ServiceOrderItemRequest
        {
            ItemType = ItemType.Service,
            Description = "Alinhamento",
            Quantity = 1,
            UnitPrice = 90m
        }, default));

        var updated = TestApi.Body(await controller.UpdateItem(
            order.Id, created.Id, new ServiceOrderItemRequest
            {
                ItemType = ItemType.Service,
                Description = "Alinhamento e balanceamento",
                Quantity = 1,
                UnitPrice = 150m
            }, default));

        Assert.Equal("Alinhamento e balanceamento", updated.Description);
        Assert.Equal(150m, updated.UnitPrice);

        var removed = await controller.DeleteItem(order.Id, created.Id, default);

        Assert.IsType<NoContentResult>(removed);
        Assert.Empty(db.ServiceOrderItems);
    }

    // D-11 writes the stock off at READY. Editing the list afterwards would
    // leave the movements disagreeing with what the order says was used.
    [Theory]
    [InlineData(ServiceOrderStatus.Ready)]
    [InlineData(ServiceOrderStatus.Delivered)]
    [InlineData(ServiceOrderStatus.Canceled)]
    public async Task ItemsAreFrozenOnceTheOrderIsClosed(ServiceOrderStatus status)
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder(status: status);
        var controller = Controller(db);

        var added = await controller.AddItem(order.Id, new ServiceOrderItemRequest
        {
            ItemType = ItemType.Service,
            Description = "Serviço tardio",
            Quantity = 1,
            UnitPrice = 10m
        }, default);

        Assert.IsType<ConflictObjectResult>(added.Result);
        Assert.Empty(db.ServiceOrderItems);
    }

    [Fact]
    public async Task RemovingAnItemFromAClosedOrderIsRefused()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.Ready);
        var item = db.AddItem(order, "Já executado", 1m, 100m);

        var result = await Controller(db).DeleteItem(order.Id, item.Id, default);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(db.ServiceOrderItems);
    }

    [Fact]
    public async Task AnItemOfAnotherOrderIsNotFound()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder();
        var stranger = db.AddItem(db.AddServiceOrder(), "De outra OS", 1m, 10m);

        var result = await Controller(db).DeleteItem(order.Id, stranger.Id, default);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    private static ServiceOrdersController Controller(AppDbContext db) =>
        new ServiceOrdersController(db).AsUser(db.AddUser());
}

public class ServiceOrderItemChangeWindowTests
{
    [Theory]
    [InlineData(ServiceOrderStatus.Requested)]
    [InlineData(ServiceOrderStatus.Confirmed)]
    [InlineData(ServiceOrderStatus.InYard)]
    [InlineData(ServiceOrderStatus.AwaitingApproval)]
    [InlineData(ServiceOrderStatus.InProgress)]
    public void ItemsChangeWhileTheWorkIsOpen(ServiceOrderStatus status)
    {
        Assert.True(ServiceOrderWorkflow.AllowsItemChanges(status));
    }

    [Theory]
    [InlineData(ServiceOrderStatus.Ready)]
    [InlineData(ServiceOrderStatus.Delivered)]
    [InlineData(ServiceOrderStatus.Canceled)]
    public void ItemsStopChangingOnceTheOrderCloses(ServiceOrderStatus status)
    {
        Assert.False(ServiceOrderWorkflow.AllowsItemChanges(status));
    }
}
