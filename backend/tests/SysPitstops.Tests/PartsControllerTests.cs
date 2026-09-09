using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SysPitstops.Api.Contracts;
using SysPitstops.Api.Controllers;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;
using Xunit;

namespace SysPitstops.Tests;

public class PartsCrudTests
{
    // Section 4 of data-model.md makes the movement the truth. A form that could
    // seed the balance would put the very first part out of step with its
    // history, with nothing to reconcile against.
    [Fact]
    public async Task ANewPartStartsWithAnEmptyBalance()
    {
        var db = TestApi.NewDatabase();

        var created = TestApi.Body(await Controller(db).Create(Request(), default));

        Assert.Equal(0m, created.QuantityOnHand);
        Assert.Empty(db.StockMovements);
    }

    [Fact]
    public async Task TheSkuIsUniqueInsideTheWorkshop()
    {
        var db = TestApi.NewDatabase();
        db.AddPart(sku: "FLT-001");

        var result = await Controller(db).Create(Request(sku: "FLT-001"), default);

        var problem = Assert.IsType<ValidationProblemDetails>(
            Assert.IsAssignableFrom<ObjectResult>(result.Result).Value);
        Assert.Contains(nameof(PartRequest.Sku), problem.Errors.Keys);
    }

    // The balance is not a field of the form, so no edit can move it. Only
    // /movements can, which is the invariant the whole section rests on.
    [Fact]
    public async Task EditingThePartLeavesTheBalanceAlone()
    {
        var db = TestApi.NewDatabase();
        var part = db.AddPart("Filtro de Óleo", salePrice: 40m, quantityOnHand: 12m);

        var updated = TestApi.Body(await Controller(db).Update(
            part.Id, Request(name: "Filtro de Óleo (original)", salePrice: 59.90m), default));

        Assert.Equal(59.90m, updated.SalePrice);
        Assert.Equal(12m, updated.QuantityOnHand);
        Assert.Empty(db.StockMovements);
    }

    [Fact]
    public async Task LowStockFlagsThePartsAtOrBelowTheMinimum()
    {
        var db = TestApi.NewDatabase();
        db.AddPart("No limite", quantityOnHand: 5m, minQuantity: 5m);
        db.AddPart("Abaixo", quantityOnHand: 1m, minQuantity: 4m);
        db.AddPart("Folgado", quantityOnHand: 20m, minQuantity: 4m);

        var page = TestApi.Body(await Controller(db).List(
            search: null, lowStock: true, includeInactive: false,
            page: null, pageSize: null, default));

        Assert.Equal(2, page.Total);
        Assert.All(page.Items, p => Assert.True(p.IsLowStock));
        Assert.DoesNotContain(page.Items, p => p.Name == "Folgado");
    }

    [Fact]
    public async Task TheSearchReadsBothTheNameAndTheSku()
    {
        var db = TestApi.NewDatabase();
        db.AddPart("Pastilha de Freio", sku: "PST-100");
        db.AddPart("Correia Dentada", sku: "COR-200");

        var byName = TestApi.Body(await Controller(db).List(
            "pastilha", false, false, null, null, default));
        var bySku = TestApi.Body(await Controller(db).List(
            "cor-2", false, false, null, null, default));

        Assert.Equal("Pastilha de Freio", Assert.Single(byName.Items).Name);
        Assert.Equal("Correia Dentada", Assert.Single(bySku.Items).Name);
    }

    // A part quoted on a past order can never be deleted — service_order_items
    // points at this row. Deactivating is what takes it out of the pickers.
    [Fact]
    public async Task DeactivatingHidesThePartWithoutLosingIt()
    {
        var db = TestApi.NewDatabase();
        var part = db.AddPart("Fora de linha");

        await Controller(db).Deactivate(part.Id, default);

        var visible = TestApi.Body(await Controller(db).List(
            null, false, false, null, null, default));
        var all = TestApi.Body(await Controller(db).List(
            null, false, includeInactive: true, null, null, default));

        Assert.Empty(visible.Items);
        Assert.False(Assert.Single(all.Items).IsActive);
    }

    private static PartRequest Request(
        string sku = "FLT-999", string name = "Filtro de Óleo", decimal salePrice = 40m) =>
        new()
        {
            Sku = sku,
            Name = name,
            Unit = "UN",
            SalePrice = salePrice,
            CostPrice = 22m,
            MinQuantity = 2m
        };

    private static PartsController Controller(AppDbContext db) =>
        new PartsController(db).AsUser(db.AddUser());
}

public class StockMovementTests
{
    [Fact]
    public async Task AnEntryAddsToTheBalanceAndLeavesARow()
    {
        var db = TestApi.NewDatabase();
        var part = db.AddPart(quantityOnHand: 4m);

        var after = TestApi.Body(await Controller(db).Move(
            part.Id, Move(MovementType.In, 6m, unitCost: 18.50m), default));

        Assert.Equal(10m, after.QuantityOnHand);

        var movement = Assert.Single(db.StockMovements);
        Assert.Equal(MovementType.In, movement.MovementType);
        Assert.Equal(6m, movement.Quantity);
        Assert.Equal(18.50m, movement.UnitCost);
        Assert.Null(movement.ServiceOrderId);
    }

    [Fact]
    public async Task AnExitSubtractsFromTheBalanceAndLeavesARow()
    {
        var db = TestApi.NewDatabase();
        var part = db.AddPart(quantityOnHand: 10m);

        var after = TestApi.Body(await Controller(db).Move(
            part.Id, Move(MovementType.Out, 3m), default));

        Assert.Equal(7m, after.QuantityOnHand);
        Assert.Equal(MovementType.Out, Assert.Single(db.StockMovements).MovementType);
    }

    // The rule of section 4, stated as a test: replaying the history has to land
    // exactly on the stored balance. If this ever fails, the cache and the audit
    // trail have drifted apart and there is no way to tell which one is right.
    [Fact]
    public async Task ReplayingTheMovementsRebuildsTheBalance()
    {
        var db = TestApi.NewDatabase();
        var part = db.AddPart();
        var controller = Controller(db);

        await controller.Move(part.Id, Move(MovementType.In, 20m), default);
        await controller.Move(part.Id, Move(MovementType.Out, 5m), default);
        await controller.Move(part.Id, Move(MovementType.In, 2m), default);
        await controller.Move(part.Id, Move(MovementType.Adjustment, 15m), default);
        await controller.Move(part.Id, Move(MovementType.Out, 4m), default);

        var replayed = db.StockMovements
            .OrderBy(m => m.CreatedAt)
            .ToList()
            .Aggregate(0m, (balance, m) => m.MovementType switch
            {
                MovementType.In => balance + m.Quantity,
                MovementType.Out => balance - m.Quantity,
                _ => m.Quantity
            });

        Assert.Equal(11m, replayed);
        Assert.Equal(replayed, db.Parts.Single(p => p.Id == part.Id).QuantityOnHand);
    }

    // D-40: the column is positive by check constraint, so a difference would
    // have nowhere to put its sign. The counted balance fits and replays.
    [Fact]
    public async Task AnAdjustmentRecordsTheCountedBalanceNotTheDifference()
    {
        var db = TestApi.NewDatabase();
        var part = db.AddPart(quantityOnHand: 30m);

        var after = TestApi.Body(await Controller(db).Move(
            part.Id, Move(MovementType.Adjustment, 27m, note: "Contagem de inventário"), default));

        Assert.Equal(27m, after.QuantityOnHand);

        var movement = Assert.Single(db.StockMovements);
        Assert.Equal(MovementType.Adjustment, movement.MovementType);
        Assert.Equal(27m, movement.Quantity);
    }

    // Counting zero is the one balance quantity > 0 cannot carry, so the row
    // recorded is the movement that reaches zero from where the part is.
    [Fact]
    public async Task CountingZeroIsRecordedAsTheExitThatEmptiesTheShelf()
    {
        var db = TestApi.NewDatabase();
        var part = db.AddPart(quantityOnHand: 8m);

        var after = TestApi.Body(await Controller(db).Move(
            part.Id, Move(MovementType.Adjustment, 0m), default));

        Assert.Equal(0m, after.QuantityOnHand);

        var movement = Assert.Single(db.StockMovements);
        Assert.Equal(MovementType.Out, movement.MovementType);
        Assert.Equal(8m, movement.Quantity);
    }

    [Fact]
    public async Task CountingZeroOnAPartThatOwesStockBringsItBackToZero()
    {
        var db = TestApi.NewDatabase();
        var part = db.AddPart(quantityOnHand: -3m);

        var after = TestApi.Body(await Controller(db).Move(
            part.Id, Move(MovementType.Adjustment, 0m), default));

        Assert.Equal(0m, after.QuantityOnHand);

        var movement = Assert.Single(db.StockMovements);
        Assert.Equal(MovementType.In, movement.MovementType);
        Assert.Equal(3m, movement.Quantity);
    }

    [Fact]
    public async Task CountingZeroOnAnEmptyPartMovesNothing()
    {
        var db = TestApi.NewDatabase();
        var part = db.AddPart(quantityOnHand: 0m);

        var after = TestApi.Body(await Controller(db).Move(
            part.Id, Move(MovementType.Adjustment, 0m), default));

        Assert.Equal(0m, after.QuantityOnHand);
        Assert.Empty(db.StockMovements);
    }

    [Fact]
    public async Task AnEntryOfNothingIsRefused()
    {
        var db = TestApi.NewDatabase();
        var part = db.AddPart();

        var result = await Controller(db).Move(part.Id, Move(MovementType.In, 0m), default);

        var problem = Assert.IsType<ValidationProblemDetails>(
            Assert.IsAssignableFrom<ObjectResult>(result.Result).Value);
        Assert.Contains(nameof(StockMovementRequest.Quantity), problem.Errors.Keys);
        Assert.Empty(db.StockMovements);
    }

    [Fact]
    public async Task TheHistoryComesBackNewestFirst()
    {
        var db = TestApi.NewDatabase();
        var part = db.AddPart();
        var controller = Controller(db);

        await controller.Move(part.Id, Move(MovementType.In, 10m), default);
        await controller.Move(part.Id, Move(MovementType.Out, 1m), default);

        var page = TestApi.Body(await controller.Movements(part.Id, null, null, default));

        Assert.Equal(2, page.Total);
        Assert.Equal(MovementType.Out, page.Items[0].MovementType);
    }

    private static StockMovementRequest Move(
        MovementType type, decimal quantity, decimal? unitCost = null, string? note = null) =>
        new() { MovementType = type, Quantity = quantity, UnitCost = unitCost, Note = note };

    private static PartsController Controller(AppDbContext db) =>
        new PartsController(db).AsUser(db.AddUser());
}

public class ServiceOrderStockWriteOffTests
{
    // D-11: the parts leave when the work is finished, not when they are added
    // to the order. There is no reservation in the MVP.
    [Fact]
    public async Task FinishingTheOrderWritesThePartsOff()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.InProgress);
        var part = db.AddPart("Óleo 5W30", quantityOnHand: 10m, costPrice: 32m);
        db.AddItem(order, "Óleo 5W30", quantity: 4m, unitPrice: 55m, part: part);

        await Controller(db).ChangeStatus(
            order.Id, new ChangeStatusRequest { ToStatus = ServiceOrderStatus.Ready }, default);

        Assert.Equal(6m, db.Parts.Single(p => p.Id == part.Id).QuantityOnHand);

        var movement = Assert.Single(db.StockMovements);
        Assert.Equal(MovementType.Out, movement.MovementType);
        Assert.Equal(4m, movement.Quantity);
        Assert.Equal(order.Id, movement.ServiceOrderId);
        // The cost of the part, not the price frozen on the item (D-07).
        Assert.Equal(32m, movement.UnitCost);
    }

    [Fact]
    public async Task NothingLeavesStockBeforeTheWorkIsFinished()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.AwaitingApproval);
        var part = db.AddPart(quantityOnHand: 10m);
        db.AddItem(order, "Peça", quantity: 2m, unitPrice: 10m, part: part);

        // Waived rather than quoted (D-13): the point here is where the stock
        // moves, not how the order got permission to start.
        await Controller(db).ChangeStatus(order.Id, new ChangeStatusRequest
        {
            ToStatus = ServiceOrderStatus.InProgress,
            WaiveApproval = true,
            Note = "Cliente autorizou por telefone."
        }, default);

        Assert.Equal(10m, db.Parts.Single(p => p.Id == part.Id).QuantityOnHand);
        Assert.Empty(db.StockMovements);
    }

    [Fact]
    public async Task TheSamePartAddedTwiceLeavesOneMovement()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.InProgress);
        var part = db.AddPart(quantityOnHand: 10m);
        db.AddItem(order, "Vela", quantity: 2m, unitPrice: 30m, part: part);
        db.AddItem(order, "Vela (a mais)", quantity: 2m, unitPrice: 30m, part: part);

        await Controller(db).ChangeStatus(
            order.Id, new ChangeStatusRequest { ToStatus = ServiceOrderStatus.Ready }, default);

        Assert.Equal(4m, Assert.Single(db.StockMovements).Quantity);
        Assert.Equal(6m, db.Parts.Single(p => p.Id == part.Id).QuantityOnHand);
    }

    [Fact]
    public async Task ServicesDoNotMoveStock()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.InProgress);
        db.AddItem(order, "Alinhamento", quantity: 1m, unitPrice: 120m);

        await Controller(db).ChangeStatus(
            order.Id, new ChangeStatusRequest { ToStatus = ServiceOrderStatus.Ready }, default);

        Assert.Empty(db.StockMovements);
    }

    // The risk D-11 accepted, written down: two orders can promise the same
    // part. Refusing here would strand an order that is physically finished, so
    // the balance goes negative and the stock list is what flags it.
    [Fact]
    public async Task TheBalanceIsAllowedToGoNegative()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.InProgress);
        var part = db.AddPart(quantityOnHand: 1m);
        db.AddItem(order, "Peça prometida duas vezes", quantity: 3m, unitPrice: 10m, part: part);

        var result = await Controller(db).ChangeStatus(
            order.Id, new ChangeStatusRequest { ToStatus = ServiceOrderStatus.Ready }, default);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(-2m, db.Parts.Single(p => p.Id == part.Id).QuantityOnHand);
        Assert.Equal(3m, Assert.Single(db.StockMovements).Quantity);
    }

    private static ServiceOrdersController Controller(AppDbContext db) =>
        new ServiceOrdersController(db).AsUser(db.AddUser());
}
