using System.ComponentModel.DataAnnotations;

namespace SysPitstops.Api.Contracts;

public record WorkshopResponse(
    int Id,
    string Name,
    string? Document,
    string? Phone,
    string? Address);

/// <summary>The name is the one field with a reader today: it is what the
/// customer sees at the top of the public quote page (D-14). The other three
/// are the workshop's own registration data.</summary>
public record WorkshopRequest
{
    [Required(ErrorMessage = "O nome da oficina é obrigatório.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 120 caracteres.")]
    public string Name { get; init; } = string.Empty;

    [StringLength(18)]
    public string? Document { get; init; }

    [StringLength(20)]
    public string? Phone { get; init; }

    [StringLength(200)]
    public string? Address { get; init; }
}

/// <summary>Changing one's own password. The current one is asked for because
/// the session cookie alone is not proof that the person at the keyboard is the
/// owner of the account — a machine left unlocked would be enough.</summary>
public record ChangePasswordRequest
{
    [Required(ErrorMessage = "A senha atual é obrigatória.")]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required(ErrorMessage = "A nova senha é obrigatória.")]
    [StringLength(100, MinimumLength = 8,
        ErrorMessage = "A senha deve ter pelo menos 8 caracteres.")]
    public string NewPassword { get; init; } = string.Empty;
}
