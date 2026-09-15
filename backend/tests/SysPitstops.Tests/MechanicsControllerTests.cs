using SysPitstops.Api.Contracts;
using SysPitstops.Api.Controllers;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;
using Xunit;

namespace SysPitstops.Tests;

public class MechanicQueueTests
{
    /// <summary>The queue is served in workflow order, not in the order the
    /// enum happens to declare (D-27 made the Postgres enum alphabetical).</summary>
    [Fact]
    public void WorkAlreadyStartedComesBeforeWorkStillWaiting()
    {
        var ranked = new[]
        {
            ServiceOrderStatus.Requested,
            ServiceOrderStatus.AwaitingApproval,
            ServiceOrderStatus.Confirmed,
            ServiceOrderStatus.InYard,
            ServiceOrderStatus.InProgress
        }.OrderBy(MechanicQueue.Rank).ToArray();

        Assert.Equal(
        [
            ServiceOrderStatus.InProgress,
            ServiceOrderStatus.InYard,
            ServiceOrderStatus.Confirmed,
            ServiceOrderStatus.AwaitingApproval,
            ServiceOrderStatus.Requested
        ], ranked);
    }

    [Fact]
    public void OnlyDiagnosisAndExecutionAreHandsOn()
    {
        Assert.True(MechanicQueue.IsHandsOn(ServiceOrderStatus.InYard));
        Assert.True(MechanicQueue.IsHandsOn(ServiceOrderStatus.InProgress));

        Assert.False(MechanicQueue.IsHandsOn(ServiceOrderStatus.Confirmed));
        Assert.False(MechanicQueue.IsHandsOn(ServiceOrderStatus.AwaitingApproval));
        Assert.False(MechanicQueue.IsHandsOn(ServiceOrderStatus.Ready));
    }

    [Fact]
    public void FinishedOrdersAreNotPending()
    {
        Assert.DoesNotContain(ServiceOrderStatus.Ready, MechanicQueue.Pending);
        Assert.DoesNotContain(ServiceOrderStatus.Delivered, MechanicQueue.Pending);
        Assert.DoesNotContain(ServiceOrderStatus.Canceled, MechanicQueue.Pending);
    }
}

public class MechanicsControllerTests
{
    [Fact]
    public async Task AMechanicExecutingAnOrderIsBusyWithIt()
    {
        var db = TestApi.NewDatabase();
        var mechanic = db.AddUser("Marcos Santos", UserRole.Mechanic);
        var order = db.AddServiceOrder(ServiceOrderStatus.InProgress, mechanic);

        var card = Assert.Single(await List(db));

        Assert.True(card.IsBusy);
        Assert.Equal(order.Id, card.Current!.Id);
        Assert.Null(card.Next);
        Assert.Equal(0, card.QueueSize);
    }

    [Fact]
    public async Task AMechanicWithOnlyCarsWaitingIsFreeAndHasANext()
    {
        var db = TestApi.NewDatabase();
        var mechanic = db.AddUser("Tiago Silva", UserRole.Mechanic);
        var order = db.AddServiceOrder(ServiceOrderStatus.Confirmed, mechanic);

        var card = Assert.Single(await List(db));

        Assert.False(card.IsBusy);
        Assert.Null(card.Current);
        Assert.Equal(order.Id, card.Next!.Id);
        Assert.Equal(1, card.QueueSize);
    }

    [Fact]
    public async Task AMechanicWithNothingAssignedIsFreeWithAnEmptyQueue()
    {
        var db = TestApi.NewDatabase();
        db.AddUser("Fernando Castro", UserRole.Mechanic);

        var card = Assert.Single(await List(db));

        Assert.False(card.IsBusy);
        Assert.Null(card.Current);
        Assert.Null(card.Next);
        Assert.Equal(0, card.QueueSize);
    }

    /// <summary>The current car is not "next" and is not in the queue: the
    /// card would otherwise count the same order twice.</summary>
    [Fact]
    public async Task TheQueueIsWhatComesAfterTheCurrentCar()
    {
        var db = TestApi.NewDatabase();
        var mechanic = db.AddUser("Marcos Santos", UserRole.Mechanic);
        var current = db.AddServiceOrder(ServiceOrderStatus.InProgress, mechanic);
        var next = db.AddServiceOrder(ServiceOrderStatus.Confirmed, mechanic);
        db.AddServiceOrder(ServiceOrderStatus.AwaitingApproval, mechanic);

        var card = Assert.Single(await List(db));

        Assert.Equal(current.Id, card.Current!.Id);
        Assert.Equal(next.Id, card.Next!.Id);
        Assert.Equal(2, card.QueueSize);
    }

    /// <summary>A car already in the yard goes before one whose customer has
    /// not answered, even when the latter was opened first.</summary>
    [Fact]
    public async Task NextFollowsTheWorkflowBeforeTheOpeningDate()
    {
        var db = TestApi.NewDatabase();
        var mechanic = db.AddUser("Tiago Silva", UserRole.Mechanic);
        var older = db.AddServiceOrder(ServiceOrderStatus.AwaitingApproval, mechanic);
        var newer = db.AddServiceOrder(ServiceOrderStatus.Confirmed, mechanic);
        older.OpenedAt = newer.OpenedAt.AddDays(-2);
        await db.SaveChangesAsync();

        var card = Assert.Single(await List(db));

        Assert.Equal(newer.Id, card.Next!.Id);
    }

    [Fact]
    public async Task WithinTheSameStatusTheOldestOrderGoesFirst()
    {
        var db = TestApi.NewDatabase();
        var mechanic = db.AddUser("Tiago Silva", UserRole.Mechanic);
        var newer = db.AddServiceOrder(ServiceOrderStatus.InProgress, mechanic);
        var older = db.AddServiceOrder(ServiceOrderStatus.InProgress, mechanic);
        older.OpenedAt = newer.OpenedAt.AddDays(-1);
        await db.SaveChangesAsync();

        var card = Assert.Single(await List(db));

        Assert.Equal(older.Id, card.Current!.Id);
        Assert.Equal(newer.Id, card.Next!.Id);
    }

    [Fact]
    public async Task FinishedOrdersLeaveTheMechanicsHands()
    {
        var db = TestApi.NewDatabase();
        var mechanic = db.AddUser("Fernando Castro", UserRole.Mechanic);
        db.AddServiceOrder(ServiceOrderStatus.Ready, mechanic);
        db.AddServiceOrder(ServiceOrderStatus.Delivered, mechanic);
        db.AddServiceOrder(ServiceOrderStatus.Canceled, mechanic);

        var card = Assert.Single(await List(db));

        Assert.False(card.IsBusy);
        Assert.Null(card.Next);
        Assert.Equal(0, card.QueueSize);
    }

    [Fact]
    public async Task AnotherMechanicsOrdersAreNotCounted()
    {
        var db = TestApi.NewDatabase();
        var tiago = db.AddUser("Tiago Silva", UserRole.Mechanic);
        var marcos = db.AddUser("Marcos Santos", UserRole.Mechanic);
        db.AddServiceOrder(ServiceOrderStatus.InProgress, marcos);
        db.AddServiceOrder(ServiceOrderStatus.Confirmed, marcos);

        var cards = await List(db);

        var tiagoCard = Assert.Single(cards, c => c.Id == tiago.Id);
        Assert.False(tiagoCard.IsBusy);
        Assert.Equal(0, tiagoCard.QueueSize);

        var marcosCard = Assert.Single(cards, c => c.Id == marcos.Id);
        Assert.True(marcosCard.IsBusy);
        Assert.Equal(1, marcosCard.QueueSize);
    }

    [Fact]
    public async Task OnlyActiveMechanicsGetACard()
    {
        var db = TestApi.NewDatabase();
        db.AddUser("Atendente", UserRole.Attendant);
        db.AddUser("Admin", UserRole.Admin);
        var gone = db.AddUser("Ex-mecânico", UserRole.Mechanic);
        gone.IsActive = false;
        await db.SaveChangesAsync();
        var mechanic = db.AddUser("Tiago Silva", UserRole.Mechanic);

        var card = Assert.Single(await List(db));

        Assert.Equal(mechanic.Id, card.Id);
    }

    [Fact]
    public async Task TheCardNamesTheCarThePrototypeShows()
    {
        var db = TestApi.NewDatabase();
        var mechanic = db.AddUser("Marcos Santos", UserRole.Mechanic);
        var order = db.AddServiceOrder(ServiceOrderStatus.InProgress, mechanic);
        var vehicle = db.Vehicles.Single(v => v.Id == order.VehicleId);
        vehicle.Brand = "Fiat";
        vehicle.Model = "Uno Mille 1.0";
        vehicle.ModelYear = null;
        await db.SaveChangesAsync();

        var card = Assert.Single(await List(db));

        Assert.Equal("Fiat Uno Mille 1.0", card.Current!.VehicleDescription);
        Assert.Equal(vehicle.Plate, card.Current.VehiclePlate);
        Assert.Equal(order.Number, card.Current.Number);
    }

    [Fact]
    public async Task CardsComeInAlphabeticalOrder()
    {
        var db = TestApi.NewDatabase();
        db.AddUser("Tiago Silva", UserRole.Mechanic);
        db.AddUser("Fernando Castro", UserRole.Mechanic);
        db.AddUser("Marcos Santos", UserRole.Mechanic);

        var names = (await List(db)).Select(c => c.Name).ToArray();

        Assert.Equal(["Fernando Castro", "Marcos Santos", "Tiago Silva"], names);
    }

    private static async Task<IReadOnlyList<MechanicWorkload>> List(AppDbContext db) =>
        TestApi.Body(await new MechanicsController(db).AsUser().List(default));
}
