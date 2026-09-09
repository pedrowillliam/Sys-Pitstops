using SysPitstops.Api.Domain;
using Xunit;
using static SysPitstops.Api.Domain.ServiceOrderStatus;
using static SysPitstops.Api.Domain.UserRole;

namespace SysPitstops.Tests;

public class ServiceOrderFlowTests
{
    private static TransitionResult Check(
        ServiceOrderStatus from,
        ServiceOrderStatus to,
        UserRole role,
        bool assigned = false,
        bool approvedQuote = false,
        bool waives = false,
        // A ordem normalmente tem responsável; a falta dele é o assunto de
        // testes próprios, não um acidente de fundo nos demais.
        bool hasMechanic = true) =>
        ServiceOrderWorkflow.Check(new ServiceOrderTransition(
            from,
            to,
            role,
            IsAssignedMechanic: assigned,
            HasMechanic: hasMechanic,
            HasApprovedQuote: approvedQuote,
            WaivesApproval: waives));

    [Theory]
    [InlineData(Requested, Confirmed)]
    [InlineData(Confirmed, InYard)]
    [InlineData(InYard, AwaitingApproval)]
    [InlineData(Ready, Delivered)]
    public void AdminWalksTheHappyPath(ServiceOrderStatus from, ServiceOrderStatus to)
    {
        Assert.True(Check(from, to, Admin).Allowed);
    }

    [Fact]
    public void FlowFollowsTheDocumentedOrder()
    {
        Assert.Equal(Confirmed, ServiceOrderWorkflow.Next(Requested));
        Assert.Equal(InYard, ServiceOrderWorkflow.Next(Confirmed));
        Assert.Equal(AwaitingApproval, ServiceOrderWorkflow.Next(InYard));
        Assert.Equal(InProgress, ServiceOrderWorkflow.Next(AwaitingApproval));
        Assert.Equal(Ready, ServiceOrderWorkflow.Next(InProgress));
        Assert.Equal(Delivered, ServiceOrderWorkflow.Next(Ready));
        Assert.Null(ServiceOrderWorkflow.Next(Delivered));
    }

    [Theory]
    [InlineData(Requested, InYard)]
    [InlineData(Requested, Ready)]
    [InlineData(InYard, Requested)]
    [InlineData(Ready, InProgress)]
    public void SkippingOrGoingBackIsRefused(ServiceOrderStatus from, ServiceOrderStatus to)
    {
        var result = Check(from, to, Admin);

        Assert.False(result.Allowed);
        Assert.Contains("Não é possível ir de", result.Reason);
    }

    [Fact]
    public void StayingInTheSameStatusIsRefused()
    {
        Assert.False(Check(InYard, InYard, Admin).Allowed);
    }

    [Theory]
    [InlineData(Delivered)]
    [InlineData(Canceled)]
    public void FinalStatusesNeverMoveAgain(ServiceOrderStatus from)
    {
        Assert.True(ServiceOrderWorkflow.IsFinal(from));
        Assert.False(Check(from, InProgress, Admin).Allowed);
        Assert.False(Check(from, Canceled, Admin).Allowed);
    }
}

public class ServiceOrderPermissionTests
{
    private static TransitionResult Check(
        ServiceOrderStatus from,
        ServiceOrderStatus to,
        UserRole role,
        bool assigned = false,
        bool approvedQuote = false,
        bool waives = false,
        // A ordem normalmente tem responsável; a falta dele é o assunto de
        // testes próprios, não um acidente de fundo nos demais.
        bool hasMechanic = true) =>
        ServiceOrderWorkflow.Check(new ServiceOrderTransition(
            from,
            to,
            role,
            IsAssignedMechanic: assigned,
            HasMechanic: hasMechanic,
            HasApprovedQuote: approvedQuote,
            WaivesApproval: waives));

    [Theory]
    [InlineData(Attendant)]
    [InlineData(Admin)]
    public void DeskConfirmsAndReceives(UserRole role)
    {
        Assert.True(Check(Requested, Confirmed, role).Allowed);
        Assert.True(Check(Confirmed, InYard, role).Allowed);
        Assert.True(Check(Ready, Delivered, role).Allowed);
    }

    [Fact]
    public void MechanicDoesNotRunTheFrontDesk()
    {
        Assert.False(Check(Requested, Confirmed, Mechanic).Allowed);
        Assert.False(Check(Confirmed, InYard, Mechanic).Allowed);
        Assert.False(Check(Ready, Delivered, Mechanic).Allowed);
    }

    [Theory]
    [InlineData(Admin)]
    [InlineData(Attendant)]
    [InlineData(Mechanic)]
    public void EveryRoleMaySendForApproval(UserRole role)
    {
        Assert.True(Check(InYard, AwaitingApproval, role).Allowed);
    }
}

public class ServiceOrderAnalysisTests
{
    private static TransitionResult ToAnalysis(UserRole role, bool hasMechanic) =>
        ServiceOrderWorkflow.Check(new ServiceOrderTransition(
            Confirmed, InYard, role, HasMechanic: hasMechanic));

    [Theory]
    [InlineData(Admin)]
    [InlineData(Attendant)]
    public void AnalysisNeedsAResponsibleMechanic(UserRole role)
    {
        var result = ToAnalysis(role, hasMechanic: false);

        Assert.False(result.Allowed);
        Assert.Contains("mecânico responsável", result.Reason);
    }

    [Theory]
    [InlineData(Admin)]
    [InlineData(Attendant)]
    public void WithAMechanicTheOrderGoesIntoAnalysis(UserRole role) =>
        Assert.True(ToAnalysis(role, hasMechanic: true).Allowed);

    /// <summary>O balcão recebe o carro antes de alguém assumir: até No Pátio a
    /// ordem anda sem responsável.</summary>
    [Fact]
    public void TheStepsBeforeAnalysisDoNotNeedOne()
    {
        Assert.True(ServiceOrderWorkflow.Check(new ServiceOrderTransition(
            Requested, Confirmed, Attendant, HasMechanic: false)).Allowed);
    }

    /// <summary>A regra é sobre a ordem ter responsável, não sobre quem clica:
    /// o mecânico continua sem poder mover a ordem para análise.</summary>
    [Fact]
    public void ItDoesNotLetTheMechanicMoveTheOrder() =>
        Assert.False(ToAnalysis(Mechanic, hasMechanic: true).Allowed);
}

public class ServiceOrderApprovalTests
{
    private static TransitionResult Start(
        UserRole role, bool approvedQuote = false, bool waives = false) =>
        ServiceOrderWorkflow.Check(new ServiceOrderTransition(
            AwaitingApproval, InProgress, role,
            HasApprovedQuote: approvedQuote, WaivesApproval: waives));

    [Fact]
    public void ApprovedQuoteStartsTheWork()
    {
        Assert.True(Start(Attendant, approvedQuote: true).Allowed);
        Assert.True(Start(Mechanic, approvedQuote: true).Allowed);
    }

    [Fact]
    public void WithoutAQuoteTheWorkDoesNotStart()
    {
        var result = Start(Attendant);

        Assert.False(result.Allowed);
        Assert.Contains("exige orçamento aprovado", result.Reason);
    }

    // D-13: the small job solved on the spot, with the reason recorded.
    [Fact]
    public void AdminMayWaiveTheApproval()
    {
        Assert.True(Start(Admin, waives: true).Allowed);
    }

    [Theory]
    [InlineData(Attendant)]
    [InlineData(Mechanic)]
    public void OnlyTheAdminMayWaiveIt(UserRole role)
    {
        Assert.False(Start(role, waives: true).Allowed);
    }
}

public class ServiceOrderCompletionTests
{
    private static TransitionResult Finish(UserRole role, bool assigned) =>
        ServiceOrderWorkflow.Check(new ServiceOrderTransition(
            InProgress, Ready, role, IsAssignedMechanic: assigned));

    [Fact]
    public void TheAssignedMechanicCloses()
    {
        Assert.True(Finish(Mechanic, assigned: true).Allowed);
    }

    [Fact]
    public void AnotherMechanicDoesNotCloseSomeoneElsesOrder()
    {
        var result = Finish(Mechanic, assigned: false);

        Assert.False(result.Allowed);
        Assert.Contains("mecânico responsável", result.Reason);
    }

    [Fact]
    public void AdminClosesRegardless()
    {
        Assert.True(Finish(Admin, assigned: false).Allowed);
    }

    [Fact]
    public void AttendantDoesNotClose()
    {
        Assert.False(Finish(Attendant, assigned: false).Allowed);
    }
}

public class ServiceOrderCancellationTests
{
    private static TransitionResult Cancel(ServiceOrderStatus from, UserRole role) =>
        ServiceOrderWorkflow.Check(new ServiceOrderTransition(from, Canceled, role));

    [Theory]
    [InlineData(Requested)]
    [InlineData(Confirmed)]
    [InlineData(InYard)]
    [InlineData(AwaitingApproval)]
    [InlineData(InProgress)]
    public void AdminCancelsAnythingBeforeReady(ServiceOrderStatus from)
    {
        Assert.True(ServiceOrderWorkflow.CanBeCanceled(from));
        Assert.True(Cancel(from, Admin).Allowed);
    }

    [Theory]
    [InlineData(Ready)]
    [InlineData(Delivered)]
    public void AfterReadyThereIsNoCancelling(ServiceOrderStatus from)
    {
        Assert.False(ServiceOrderWorkflow.CanBeCanceled(from));
        Assert.False(Cancel(from, Admin).Allowed);
    }

    [Theory]
    [InlineData(Attendant)]
    [InlineData(Mechanic)]
    public void OnlyTheAdminCancels(UserRole role)
    {
        var result = Cancel(InYard, role);

        Assert.False(result.Allowed);
        Assert.Contains("administrador", result.Reason);
    }
}
