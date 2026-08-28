using Microsoft.EntityFrameworkCore;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;

namespace SysPitstops.Api.Auth;

public static class AdminSeeder
{
    public const int MvpWorkshopId = 1;

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(AdminSeeder));

        if (!await db.Workshops.AnyAsync(w => w.Id == MvpWorkshopId, ct))
        {
            db.Workshops.Add(new Workshop { Id = MvpWorkshopId, Name = "Oficina Piloto" });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Workshop {Id} created.", MvpWorkshopId);
        }

        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin, ct))
        {
            return;
        }

        var email = config["SEED_ADMIN_EMAIL"];
        var password = config["SEED_ADMIN_PASSWORD"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "No admin exists and SEED_ADMIN_EMAIL/SEED_ADMIN_PASSWORD are not set. "
                + "Nobody can sign in until they are.");
            return;
        }

        db.Users.Add(new User
        {
            WorkshopId = MvpWorkshopId,
            Name = "Administrador",
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = hasher.Hash(password),
            Role = UserRole.Admin
        });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("First admin created: {Email}", email);
    }
}
