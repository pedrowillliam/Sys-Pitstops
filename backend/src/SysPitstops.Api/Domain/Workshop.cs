namespace SysPitstops.Api.Domain;

// Multi-tenant hedge: the MVP always uses id = 1 (see docs/decisions.md, D-02).
public class Workshop
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Document { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
