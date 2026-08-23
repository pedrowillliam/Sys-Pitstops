namespace SysPitstops.Api.Domain;

public class User
{
    public Guid Id { get; set; }
    public int WorkshopId { get; set; }
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Workshop Workshop { get; set; } = null!;
}
