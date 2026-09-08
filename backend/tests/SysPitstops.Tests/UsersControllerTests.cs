using Microsoft.AspNetCore.Mvc;
using SysPitstops.Api.Auth;
using SysPitstops.Api.Contracts;
using SysPitstops.Api.Controllers;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;
using Xunit;

namespace SysPitstops.Tests;

public class UsersControllerTests
{
    [Fact]
    public async Task CreatingAMechanicStoresAHashedPassword()
    {
        var db = TestApi.NewDatabase();

        var created = TestApi.Body(await Controller(db).Create(new CreateUserRequest
        {
            Name = "Roberto Silva",
            Email = "  Roberto@Oficina.local ",
            Password = "senha-forte-123",
            Role = UserRole.Mechanic
        }, default));

        Assert.Equal(UserRole.Mechanic, created.Role);
        Assert.Equal("roberto@oficina.local", created.Email);

        var stored = db.Users.Single(u => u.Id == created.Id);
        Assert.NotEqual("senha-forte-123", stored.PasswordHash);
        Assert.True(new BCryptPasswordHasher().Verify("senha-forte-123", stored.PasswordHash));
    }

    [Fact]
    public async Task TheSameEmailTwiceIsAConflict()
    {
        var db = TestApi.NewDatabase();
        var controller = Controller(db);
        var request = new CreateUserRequest
        {
            Name = "Roberto",
            Email = "roberto@oficina.local",
            Password = "senha-forte-123",
            Role = UserRole.Mechanic
        };

        await controller.Create(request, default);
        var again = await controller.Create(request with { Name = "Outro" }, default);

        Assert.IsType<ConflictObjectResult>(again.Result);
        Assert.Single(db.Users);
    }

    [Fact]
    public async Task ListingFiltersByRoleAndHidesInactive()
    {
        var db = TestApi.NewDatabase();
        db.AddUser("Mecânico ativo", UserRole.Mechanic);
        db.AddUser("Atendente", UserRole.Attendant);

        var inactive = db.AddUser("Mecânico inativo", UserRole.Mechanic);
        inactive.IsActive = false;
        await db.SaveChangesAsync();

        var mechanics = TestApi.Body(await Controller(db).List(UserRole.Mechanic, false, default));

        var only = Assert.Single(mechanics);
        Assert.Equal("Mecânico ativo", only.Name);
    }

    [Fact]
    public async Task ListingSeesOnlyItsOwnWorkshop()
    {
        var db = TestApi.NewDatabase();
        db.AddUser("Daqui", UserRole.Mechanic);

        var stranger = db.AddUser("De outra oficina", UserRole.Mechanic);
        stranger.WorkshopId = 99;
        await db.SaveChangesAsync();

        var mechanics = TestApi.Body(await Controller(db).List(UserRole.Mechanic, false, default));

        Assert.Single(mechanics);
    }

    private static UsersController Controller(AppDbContext db) =>
        new UsersController(db, new BCryptPasswordHasher()).AsUser();
}
