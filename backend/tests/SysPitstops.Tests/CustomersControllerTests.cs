using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SysPitstops.Api.Contracts;
using SysPitstops.Api.Controllers;
using SysPitstops.Api.Data;
using Xunit;

namespace SysPitstops.Tests;

public class CustomersControllerTests
{
    private static CustomerRequest Valid(string name = "João da Silva") => new()
    {
        Name = name,
        Phone = "(81) 99999-0000",
        Document = "529.982.247-25",
        Email = "Joao@Exemplo.com "
    };

    private static (AppDbContext Db, CustomersController Controller) Build()
    {
        var db = TestApi.NewDatabase();
        return (db, new CustomersController(db).AsUser());
    }

    [Fact]
    public async Task CreateStoresPhoneAndDocumentWithDigitsOnly()
    {
        var (db, controller) = Build();

        var created = TestApi.Body(await controller.Create(Valid(), default));

        Assert.Equal("81999990000", created.Phone);
        Assert.Equal("52998224725", created.Document);
        Assert.Equal("joao@exemplo.com", created.Email);
        Assert.True(created.IsActive);
        Assert.Empty(created.Vehicles);
        Assert.Single(db.Customers);
    }

    /// <summary>Copying the number from the phone book brings the +55 along.
    /// It is dropped on save so the stored digits stay comparable.</summary>
    [Fact]
    public async Task CreateAcceptsANumberPastedWithTheCountryCode()
    {
        var (_, controller) = Build();

        var created = TestApi.Body(
            await controller.Create(Valid() with { Phone = "+55 (81) 99999-0000" }, default));

        Assert.Equal("81999990000", created.Phone);
    }

    [Fact]
    public async Task CreateRejectsAPhoneWithoutAreaCode()
    {
        var (db, controller) = Build();

        var result = await controller.Create(Valid() with { Phone = "99999-0000" }, default);

        result.Result.AssertRejects("Phone");
        Assert.Empty(db.Customers);
    }

    [Fact]
    public async Task CreateRejectsADocumentWithAWrongCheckDigit()
    {
        var (db, controller) = Build();

        var result = await controller.Create(Valid() with { Document = "529.982.247-26" }, default);

        result.Result.AssertRejects("Document");
        Assert.Empty(db.Customers);
    }

    [Fact]
    public async Task DocumentIsOptional()
    {
        var (_, controller) = Build();

        var created = TestApi.Body(await controller.Create(Valid() with { Document = "  " }, default));

        Assert.Null(created.Document);
    }

    [Fact]
    public async Task GetReturnsTheCreatedCustomer()
    {
        var (_, controller) = Build();
        var created = TestApi.Body(await controller.Create(Valid(), default));

        var found = TestApi.Body(await controller.Get(created.Id, default));

        Assert.Equal(created.Id, found.Id);
        Assert.Equal("João da Silva", found.Name);
    }

    [Fact]
    public async Task GetOfAnUnknownIdIsNotFound()
    {
        var (_, controller) = Build();

        var result = await controller.Get(Guid.NewGuid(), default);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateChangesTheFieldsAndMovesUpdatedAt()
    {
        var (db, controller) = Build();
        var created = TestApi.Body(await controller.Create(Valid(), default));

        var updated = TestApi.Body(await controller.Update(
            created.Id,
            Valid("Maria Souza") with { Phone = "81 3333-4444", Document = null },
            default));

        Assert.Equal("Maria Souza", updated.Name);
        Assert.Equal("8133334444", updated.Phone);
        Assert.Null(updated.Document);
        Assert.True(updated.UpdatedAt >= created.UpdatedAt);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);
    }

    [Fact]
    public async Task UpdateOfAnUnknownIdIsNotFound()
    {
        var (_, controller) = Build();

        var result = await controller.Update(Guid.NewGuid(), Valid(), default);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task ListFindsByNameAndByPhone()
    {
        var (_, controller) = Build();
        await controller.Create(Valid("João da Silva"), default);
        await controller.Create(
            Valid("Maria Souza") with { Phone = "81 98888-1111", Document = null }, default);

        var byName = TestApi.Body(await controller.List("maria", false, null, null, default));
        var byPhone = TestApi.Body(await controller.List("98888", false, null, null, default));

        Assert.Equal("Maria Souza", Assert.Single(byName.Items).Name);
        Assert.Equal("Maria Souza", Assert.Single(byPhone.Items).Name);
    }

    [Fact]
    public async Task ListHidesDeactivatedCustomersUnlessAsked()
    {
        var (_, controller) = Build();
        var created = TestApi.Body(await controller.Create(Valid(), default));

        await controller.Deactivate(created.Id, default);

        var visible = TestApi.Body(await controller.List(null, false, null, null, default));
        var all = TestApi.Body(await controller.List(null, true, null, null, default));

        Assert.Empty(visible.Items);
        Assert.False(Assert.Single(all.Items).IsActive);
    }

    [Fact]
    public async Task ReactivateBringsTheCustomerBack()
    {
        var (_, controller) = Build();
        var created = TestApi.Body(await controller.Create(Valid(), default));
        await controller.Deactivate(created.Id, default);

        Assert.IsType<NoContentResult>(await controller.Reactivate(created.Id, default));

        var found = TestApi.Body(await controller.Get(created.Id, default));
        Assert.True(found.IsActive);
    }

    [Fact]
    public async Task ListPagesAndClampsThePageSize()
    {
        var (_, controller) = Build();
        for (var i = 0; i < 3; i++)
        {
            await controller.Create(
                Valid($"Cliente {i}") with { Document = null }, default);
        }

        var firstPage = TestApi.Body(await controller.List(null, false, 1, 2, default));
        var secondPage = TestApi.Body(await controller.List(null, false, 2, 2, default));
        var clamped = TestApi.Body(await controller.List(null, false, 0, 10_000, default));

        Assert.Equal(3, firstPage.Total);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Single(secondPage.Items);
        Assert.Equal(1, clamped.Page);
        Assert.Equal(PagedResult<CustomerResponse>.MaxPageSize, clamped.PageSize);
    }

    [Fact]
    public async Task CustomerCarriesItsVehicles()
    {
        var db = TestApi.NewDatabase();
        var customer = db.AddCustomer("Dono");
        var vehicles = new VehiclesController(db).AsUser();
        await vehicles.Create(new VehicleRequest
        {
            OwnerId = customer.Id,
            Plate = "abc-1d23",
            Brand = "Fiat",
            Model = "Uno"
        }, default);

        var found = TestApi.Body(await new CustomersController(db).AsUser().Get(customer.Id, default));

        var vehicle = Assert.Single(found.Vehicles);
        Assert.Equal("Fiat", vehicle.Brand);
        Assert.Equal("Uno", vehicle.Model);
        // The controller normalises the plate; the card shows what was stored.
        Assert.Equal("ABC1D23", vehicle.Plate);
    }
}

internal static class ValidationProblemAssertions
{
    public static void AssertRejects(this IActionResult? result, string field)
    {
        var body = Assert.IsAssignableFrom<ObjectResult>(result);
        var problem = Assert.IsType<ValidationProblemDetails>(body.Value);

        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Contains(field, problem.Errors.Keys);
    }
}
