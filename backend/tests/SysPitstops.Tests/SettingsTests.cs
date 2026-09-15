using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SysPitstops.Api.Auth;
using SysPitstops.Api.Contracts;
using SysPitstops.Api.Controllers;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;
using Xunit;

namespace SysPitstops.Tests;

public class WorkshopControllerTests
{
    private static WorkshopRequest Valid() => new()
    {
        Name = "  Oficina do Pedro  ",
        Document = "11.222.333/0001-81",
        Phone = "(81) 3333-4444",
        Address = "Rua das Oficinas, 100"
    };

    [Fact]
    public async Task TheNameIsTrimmedAndTheRestStoredAsDigits()
    {
        var db = TestApi.NewDatabase();

        var saved = TestApi.Body(await Controller(db).Update(Valid(), default));

        Assert.Equal("Oficina do Pedro", saved.Name);
        Assert.Equal("11222333000181", saved.Document);
        Assert.Equal("8133334444", saved.Phone);
    }

    /// <summary>The name is what the customer reads at the top of the public
    /// quote, so an empty one would blank that page.</summary>
    [Fact]
    public async Task TheNameIsRequired()
    {
        var db = TestApi.NewDatabase();

        var result = await Controller(db).Update(Valid() with { Name = " " }, default);

        result.Result.AssertRejects("Name");
    }

    [Fact]
    public async Task AnInvalidDocumentIsRefused()
    {
        var db = TestApi.NewDatabase();

        var result = await Controller(db).Update(
            Valid() with { Document = "11.222.333/0001-99" }, default);

        result.Result.AssertRejects("Document");
    }

    [Fact]
    public async Task AnInvalidPhoneIsRefused()
    {
        var db = TestApi.NewDatabase();

        var result = await Controller(db).Update(Valid() with { Phone = "3333-4444" }, default);

        result.Result.AssertRejects("Phone");
    }

    /// <summary>Empty stays null, so "has no document" does not become "has a
    /// blank one" — the same rule the customer form follows.</summary>
    [Fact]
    public async Task TheOptionalFieldsMayBeCleared()
    {
        var db = TestApi.NewDatabase();
        var controller = Controller(db);

        await controller.Update(Valid(), default);
        var cleared = TestApi.Body(await controller.Update(
            new WorkshopRequest { Name = "Oficina", Document = "", Phone = null, Address = "  " },
            default));

        Assert.Null(cleared.Document);
        Assert.Null(cleared.Phone);
        Assert.Null(cleared.Address);
    }

    [Fact]
    public async Task ThePublicQuoteShowsTheNameThatWasSaved()
    {
        var db = TestApi.NewDatabase();
        await Controller(db).Update(Valid() with { Name = "Oficina do Pedro" }, default);

        var workshop = await db.Workshops.SingleAsync();

        Assert.Equal("Oficina do Pedro", workshop.Name);
    }

    private static WorkshopController Controller(AppDbContext db) =>
        new WorkshopController(db).AsUser();
}

public class ChangePasswordTests
{
    [Fact]
    public async Task TheCurrentPasswordIsReplaced()
    {
        var (db, hasher, user) = Build("senha-antiga");

        var result = await Controller(db, hasher, user).ChangePassword(
            new ChangePasswordRequest
            {
                CurrentPassword = "senha-antiga",
                NewPassword = "senha-nova-123"
            },
            default);

        Assert.IsType<NoContentResult>(result);
        Assert.True(hasher.Verify("senha-nova-123", db.Users.Single().PasswordHash));
    }

    /// <summary>The cookie proves the browser signed in once, not that whoever
    /// is typing now owns the account.</summary>
    [Fact]
    public async Task TheWrongCurrentPasswordChangesNothing()
    {
        var (db, hasher, user) = Build("senha-antiga");

        var result = await Controller(db, hasher, user).ChangePassword(
            new ChangePasswordRequest
            {
                CurrentPassword = "chute",
                NewPassword = "senha-nova-123"
            },
            default);

        result.AssertRejects("CurrentPassword");
        Assert.True(hasher.Verify("senha-antiga", db.Users.Single().PasswordHash));
    }

    [Fact]
    public async Task RepeatingTheSamePasswordIsRefused()
    {
        var (db, hasher, user) = Build("senha-antiga");

        var result = await Controller(db, hasher, user).ChangePassword(
            new ChangePasswordRequest
            {
                CurrentPassword = "senha-antiga",
                NewPassword = "senha-antiga"
            },
            default);

        result.AssertRejects("NewPassword");
    }

    [Fact]
    public async Task AnInactiveUserCannotChangeIt()
    {
        var (db, hasher, user) = Build("senha-antiga");
        user.IsActive = false;
        await db.SaveChangesAsync();

        var result = await Controller(db, hasher, user).ChangePassword(
            new ChangePasswordRequest
            {
                CurrentPassword = "senha-antiga",
                NewPassword = "senha-nova-123"
            },
            default);

        Assert.IsType<UnauthorizedResult>(result);
    }

    private static (AppDbContext Db, IPasswordHasher Hasher, User User) Build(string password)
    {
        var db = TestApi.NewDatabase();
        var hasher = new BCryptPasswordHasher();
        var user = db.AddUser();

        user.PasswordHash = hasher.Hash(password);
        db.SaveChanges();

        return (db, hasher, user);
    }

    private static AuthController Controller(AppDbContext db, IPasswordHasher hasher, User user) =>
        new AuthController(db, hasher, null!, null!).AsUser(user);
}
