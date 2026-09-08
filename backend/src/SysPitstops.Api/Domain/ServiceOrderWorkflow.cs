namespace SysPitstops.Api.Domain;

public readonly record struct ServiceOrderTransition(
    ServiceOrderStatus From,
    ServiceOrderStatus To,
    UserRole Role,
    bool IsAssignedMechanic = false,
    bool HasApprovedQuote = false,
    bool WaivesApproval = false);

public readonly record struct TransitionResult(bool Allowed, string? Reason)
{
    public static TransitionResult Allow() => new(true, null);

    public static TransitionResult Refuse(string reason) => new(false, reason);
}

public static class ServiceOrderWorkflow
{
    private static readonly ServiceOrderStatus[] Flow =
    [
        ServiceOrderStatus.Requested,
        ServiceOrderStatus.Confirmed,
        ServiceOrderStatus.InYard,
        ServiceOrderStatus.AwaitingApproval,
        ServiceOrderStatus.InProgress,
        ServiceOrderStatus.Ready,
        ServiceOrderStatus.Delivered
    ];

    public static bool IsFinal(ServiceOrderStatus status) =>
        status is ServiceOrderStatus.Delivered or ServiceOrderStatus.Canceled;

    public static bool CanBeCanceled(ServiceOrderStatus status) =>
        !IsFinal(status) && status != ServiceOrderStatus.Ready;

    public static bool AllowsItemChanges(ServiceOrderStatus status) =>
        !IsFinal(status) && status != ServiceOrderStatus.Ready;

    public static ServiceOrderStatus? Next(ServiceOrderStatus status)
    {
        var index = Array.IndexOf(Flow, status);
        return index >= 0 && index < Flow.Length - 1 ? Flow[index + 1] : null;
    }

    public static TransitionResult Check(ServiceOrderTransition transition)
    {
        var (from, to) = (transition.From, transition.To);

        if (from == to)
        {
            return TransitionResult.Refuse($"A ordem de serviço já está em {Label(to)}.");
        }

        if (IsFinal(from))
        {
            return TransitionResult.Refuse(
                $"Uma ordem de serviço em {Label(from)} não muda mais de status.");
        }

        if (to == ServiceOrderStatus.Canceled)
        {
            return CheckCancellation(transition);
        }

        if (Next(from) != to)
        {
            return TransitionResult.Refuse(
                $"Não é possível ir de {Label(from)} para {Label(to)}.");
        }

        return to switch
        {
            ServiceOrderStatus.Confirmed => RequireDesk(transition.Role),
            ServiceOrderStatus.InYard => RequireDesk(transition.Role),
            ServiceOrderStatus.AwaitingApproval => TransitionResult.Allow(),
            ServiceOrderStatus.InProgress => CheckStart(transition),
            ServiceOrderStatus.Ready => CheckFinish(transition),
            ServiceOrderStatus.Delivered => RequireDesk(transition.Role),
            _ => TransitionResult.Refuse($"Transição para {Label(to)} não é suportada.")
        };
    }

    private static TransitionResult CheckCancellation(ServiceOrderTransition transition)
    {
        if (!CanBeCanceled(transition.From))
        {
            return TransitionResult.Refuse(
                $"Uma ordem de serviço em {Label(transition.From)} não pode mais ser cancelada.");
        }

        return transition.Role == UserRole.Admin
            ? TransitionResult.Allow()
            : TransitionResult.Refuse("Apenas o administrador pode cancelar uma ordem de serviço.");
    }

    private static TransitionResult CheckStart(ServiceOrderTransition transition)
    {
        if (transition.HasApprovedQuote)
        {
            return TransitionResult.Allow();
        }

        if (transition.WaivesApproval && transition.Role == UserRole.Admin)
        {
            return TransitionResult.Allow();
        }

        return TransitionResult.Refuse(
            "A execução exige orçamento aprovado. O administrador pode dispensar a aprovação "
            + "registrando o motivo.");
    }

    private static TransitionResult CheckFinish(ServiceOrderTransition transition)
    {
        if (transition.Role == UserRole.Admin)
        {
            return TransitionResult.Allow();
        }

        if (transition.Role == UserRole.Mechanic && transition.IsAssignedMechanic)
        {
            return TransitionResult.Allow();
        }

        return TransitionResult.Refuse(
            "Apenas o mecânico responsável pela ordem de serviço ou o administrador "
            + "pode concluí-la.");
    }

    private static TransitionResult RequireDesk(UserRole role) =>
        role is UserRole.Admin or UserRole.Attendant
            ? TransitionResult.Allow()
            : TransitionResult.Refuse("Seu perfil não pode fazer esta transição.");

    private static string Label(ServiceOrderStatus status) => status.ToPgName();
}
