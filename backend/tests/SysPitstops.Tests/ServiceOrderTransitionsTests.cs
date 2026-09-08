using Microsoft.AspNetCore.Mvc;
using SysPitstops.Api.Contracts;
using SysPitstops.Api.Controllers;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;
using Xunit;

namespace SysPitstops.Tests;

public class ServiceOrderFullFlowTests
{
    // The verification milestone of week 2: the order walks the whole flow and
    // every step leaves a line behind.
    [Fact]
    public async Task AnOrderWalksFromRequestedToDelivered()
    {
        var db = TestApi.NewDatabase();
        var mechanic = db.AddUser("Roberto", UserRole.Mechanic);
        var vehicle = db.AddVehicle(db.AddCustomer("João"), "GHJ4K56", "Fiat", "Strada", 2021);
        var admin = new ServiceOrdersController(db).AsUser(db.AddUser());

        var order = TestApi.Body(await admin.Open(
            new OpenServiceOrderRequest { VehicleId = vehicle.Id }, default));

        await admin.Update(order.Id, new UpdateServiceOrderRequest { MechanicId = mechanic.Id }, default);

        await Move(admin, order.Id, ServiceOrderStatus.Confirmed);
        await Move(admin, order.Id, ServiceOrderStatus.InYard);
        await Move(admin, order.Id, ServiceOrderStatus.AwaitingApproval);

        // No quote exists in week 2, so the admin waiver of D-13 is the only way in.
        await Move(admin, order.Id, ServiceOrderStatus.InProgress, waive: true,
            note: "Serviço pequeno resolvido na hora.");

        await Move(admin, order.Id, ServiceOrderStatus.Ready);
        var delivered = TestApi.Body(await Move(admin, order.Id, ServiceOrderStatus.Delivered));

        Assert.Equal(ServiceOrderStatus.Delivered, delivered.Status);
        Assert.Equal("Serviço pequeno resolvido na hora.", delivered.ApprovalWaivedNote);
        Assert.NotNull(delivered.ClosedAt);

        // Opening plus six moves.
        Assert.Equal(7, delivered.History.Count);
        Assert.Null(delivered.History[0].FromStatus);
        Assert.Equal(
        [
            ServiceOrderStatus.Requested,
            ServiceOrderStatus.Confirmed,
            ServiceOrderStatus.InYard,
            ServiceOrderStatus.AwaitingApproval,
            ServiceOrderStatus.InProgress,
            ServiceOrderStatus.Ready,
            ServiceOrderStatus.Delivered
        ], delivered.History.Select(h => h.ToStatus));
    }

    [Fact]
    public async Task EachMoveRecordsWhereItCameFrom()
    {
        var db = TestApi.NewDatabase();
        var admin = new ServiceOrdersController(db).AsUser(db.AddUser());
        var order = db.AddServiceOrder();

        var moved = TestApi.Body(await Move(admin, order.Id, ServiceOrderStatus.Confirmed));

        var entry = Assert.Single(moved.History);
        Assert.Equal(ServiceOrderStatus.Requested, entry.FromStatus);
        Assert.Equal(ServiceOrderStatus.Confirmed, entry.ToStatus);
    }

    // The timestamp of this transition is what the average execution time reads.
    [Fact]
    public async Task ReachingReadyClosesTheOrder()
    {
        var db = TestApi.NewDatabase();
        var admin = new ServiceOrdersController(db).AsUser(db.AddUser());
        var order = db.AddServiceOrder(status: ServiceOrderStatus.InProgress);

        var ready = TestApi.Body(await Move(admin, order.Id, ServiceOrderStatus.Ready));

        Assert.NotNull(ready.ClosedAt);
    }

    [Fact]
    public async Task CancellingLeavesTheOrderOpenEnded()
    {
        var db = TestApi.NewDatabase();
        var admin = new ServiceOrdersController(db).AsUser(db.AddUser());
        var order = db.AddServiceOrder(status: ServiceOrderStatus.InYard);

        var canceled = TestApi.Body(await Move(
            admin, order.Id, ServiceOrderStatus.Canceled, note: "Cliente desistiu."));

        Assert.Equal(ServiceOrderStatus.Canceled, canceled.Status);
        Assert.Null(canceled.ClosedAt);
        Assert.Equal("Cliente desistiu.", canceled.History[^1].Note);
    }

    private static async Task<ActionResult<ServiceOrderDetail>> Move(
        ServiceOrdersController controller,
        Guid id,
        ServiceOrderStatus to,
        bool waive = false,
        string? note = null) =>
        await controller.ChangeStatus(
            id, new ChangeStatusRequest { ToStatus = to, WaiveApproval = waive, Note = note },
            default);
}

public class ServiceOrderTransitionRefusalTests
{
    [Fact]
    public async Task AnInvalidTransitionAnswersConflictWithTheReason()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder();   // REQUESTED

        var result = await Controller(db).ChangeStatus(
            order.Id, new ChangeStatusRequest { ToStatus = ServiceOrderStatus.Ready }, default);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Contains("Não é possível ir de", problem.Title);
    }

    [Fact]
    public async Task StartingWorkWithoutAQuoteIsRefused()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.AwaitingApproval);

        var result = await Controller(db).ChangeStatus(
            order.Id, new ChangeStatusRequest { ToStatus = ServiceOrderStatus.InProgress }, default);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Contains("exige orçamento aprovado", ((ProblemDetails)conflict.Value!).Title);
    }

    // D-13 trades the block for a recorded reason; an empty note would leave the
    // waiver unaudited, which is the one thing the rule exists to prevent.
    [Fact]
    public async Task WaivingWithoutAReasonIsRefused()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.AwaitingApproval);

        var result = await Controller(db).ChangeStatus(order.Id, new ChangeStatusRequest
        {
            ToStatus = ServiceOrderStatus.InProgress,
            WaiveApproval = true,
            Note = "   "
        }, default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(ServiceOrderStatus.AwaitingApproval, db.ServiceOrders.Single().Status);
    }

    [Fact]
    public async Task AMechanicWhoIsNotResponsibleCannotFinish()
    {
        var db = TestApi.NewDatabase();
        var responsible = db.AddUser("Responsável", UserRole.Mechanic);
        var stranger = db.AddUser("Outro", UserRole.Mechanic);
        var order = db.AddServiceOrder(status: ServiceOrderStatus.InProgress, mechanic: responsible);

        var result = await new ServiceOrdersController(db).AsUser(stranger).ChangeStatus(
            order.Id, new ChangeStatusRequest { ToStatus = ServiceOrderStatus.Ready }, default);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Contains("mecânico responsável", ((ProblemDetails)conflict.Value!).Title);
    }

    [Fact]
    public async Task TheResponsibleMechanicFinishes()
    {
        var db = TestApi.NewDatabase();
        var mechanic = db.AddUser("Responsável", UserRole.Mechanic);
        var order = db.AddServiceOrder(status: ServiceOrderStatus.InProgress, mechanic: mechanic);

        var result = await new ServiceOrdersController(db).AsUser(mechanic).ChangeStatus(
            order.Id, new ChangeStatusRequest { ToStatus = ServiceOrderStatus.Ready }, default);

        Assert.Equal(ServiceOrderStatus.Ready, TestApi.Body(result).Status);
    }

    private static ServiceOrdersController Controller(AppDbContext db) =>
        new ServiceOrdersController(db).AsUser(db.AddUser());
}

public class AllowedTransitionsTests
{
    [Fact]
    public async Task TheAdminSeesTheNextStepAndTheCancellation()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.InYard);

        var allowed = TestApi.Body(
            await new ServiceOrdersController(db).AsUser(db.AddUser()).AllowedTransitions(
                order.Id, default));

        Assert.Equal(
            [ServiceOrderStatus.AwaitingApproval, ServiceOrderStatus.Canceled],
            allowed.Select(a => a.ToStatus));
        Assert.All(allowed, a => Assert.False(a.RequiresNote));
    }

    // Without a quote the only way forward is the waiver, and the board has to
    // know it must ask for a reason before offering the button.
    [Fact]
    public async Task StartingWorkIsOfferedToTheAdminOnlyWithANote()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.AwaitingApproval);

        var allowed = TestApi.Body(
            await new ServiceOrdersController(db).AsUser(db.AddUser()).AllowedTransitions(
                order.Id, default));

        var start = Assert.Single(allowed, a => a.ToStatus == ServiceOrderStatus.InProgress);
        Assert.True(start.RequiresNote);
    }

    [Fact]
    public async Task TheAttendantIsNotOfferedWhatItCannotDo()
    {
        var db = TestApi.NewDatabase();
        var attendant = db.AddUser("Atendente", UserRole.Attendant);
        var order = db.AddServiceOrder(status: ServiceOrderStatus.AwaitingApproval);

        var allowed = TestApi.Body(
            await new ServiceOrdersController(db).AsUser(attendant).AllowedTransitions(
                order.Id, default));

        // No quote, and only the admin may waive it, so nothing moves forward.
        Assert.DoesNotContain(allowed, a => a.ToStatus == ServiceOrderStatus.InProgress);
        Assert.DoesNotContain(allowed, a => a.ToStatus == ServiceOrderStatus.Canceled);
    }

    [Fact]
    public async Task AFinishedOrderOffersNothing()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.Delivered);

        var allowed = TestApi.Body(
            await new ServiceOrdersController(db).AsUser(db.AddUser()).AllowedTransitions(
                order.Id, default));

        Assert.Empty(allowed);
    }
}

public class ServiceOrderUpdateTests
{
    [Fact]
    public async Task AssigningAMechanicIsWhatMakesTheOrderFinishable()
    {
        var db = TestApi.NewDatabase();
        var mechanic = db.AddUser("Roberto", UserRole.Mechanic);
        var order = db.AddServiceOrder();

        var updated = TestApi.Body(await Controller(db).Update(
            order.Id, new UpdateServiceOrderRequest { MechanicId = mechanic.Id }, default));

        Assert.Equal(mechanic.Id, updated.MechanicId);
        Assert.Equal("Roberto", updated.MechanicName);
    }

    [Fact]
    public async Task AnAttendantCannotBeTheResponsibleMechanic()
    {
        var db = TestApi.NewDatabase();
        var attendant = db.AddUser("Atendente", UserRole.Attendant);
        var order = db.AddServiceOrder();

        var result = await Controller(db).Update(
            order.Id, new UpdateServiceOrderRequest { MechanicId = attendant.Id }, default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task TheDiscountReachesTheTotal()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder();
        db.AddItem(order, "Serviço", 1m, 200m);

        var updated = TestApi.Body(await Controller(db).Update(
            order.Id, new UpdateServiceOrderRequest { DiscountAmount = 50m }, default));

        Assert.Equal(150m, updated.Total);
    }

    [Fact]
    public async Task AFinishedOrderNoLongerChanges()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.Delivered);

        var result = await Controller(db).Update(
            order.Id, new UpdateServiceOrderRequest { DiscountAmount = 10m }, default);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    private static ServiceOrdersController Controller(AppDbContext db) =>
        new ServiceOrdersController(db).AsUser(db.AddUser());
}

public class ServiceOrderDiagnosisTests
{
    [Fact]
    public async Task TheResponsibleMechanicWritesTheDiagnosis()
    {
        var db = TestApi.NewDatabase();
        var mechanic = db.AddUser("Roberto", UserRole.Mechanic);
        var order = db.AddServiceOrder(status: ServiceOrderStatus.InProgress, mechanic: mechanic);

        var updated = TestApi.Body(await new ServiceOrdersController(db).AsUser(mechanic)
            .UpdateDiagnosis(
                order.Id,
                new UpdateDiagnosisRequest { Diagnosis = "  Pastilhas no limite.  " },
                default));

        Assert.Equal("Pastilhas no limite.", updated.Diagnosis);
    }

    [Fact]
    public async Task AnotherMechanicDoesNotWriteOnSomeoneElsesOrder()
    {
        var db = TestApi.NewDatabase();
        var responsible = db.AddUser("Responsável", UserRole.Mechanic);
        var stranger = db.AddUser("Outro", UserRole.Mechanic);
        var order = db.AddServiceOrder(status: ServiceOrderStatus.InProgress, mechanic: responsible);

        var result = await new ServiceOrdersController(db).AsUser(stranger)
            .UpdateDiagnosis(order.Id, new UpdateDiagnosisRequest { Diagnosis = "..." }, default);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task TheFrontDeskMayAlsoWriteIt()
    {
        var db = TestApi.NewDatabase();
        var attendant = db.AddUser("Atendente", UserRole.Attendant);
        var order = db.AddServiceOrder(status: ServiceOrderStatus.InYard);

        var updated = TestApi.Body(await new ServiceOrdersController(db).AsUser(attendant)
            .UpdateDiagnosis(
                order.Id, new UpdateDiagnosisRequest { Diagnosis = "Relato do cliente." }, default));

        Assert.Equal("Relato do cliente.", updated.Diagnosis);
    }
}
