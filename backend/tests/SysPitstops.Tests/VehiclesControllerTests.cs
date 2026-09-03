using Microsoft.AspNetCore.Mvc;
using SysPitstops.Api.Contracts;
using SysPitstops.Api.Controllers;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;
using Xunit;

namespace SysPitstops.Tests;

public class VehiclesControllerTests
{
    private static VehicleRequest Valid(Guid ownerId, string plate = "abc-1d23") => new()
    {
        OwnerId = ownerId,
        Plate = plate,
        Brand = "Fiat",
        Model = "Uno",
        ModelYear = 2018,
        Color = "Branco"
    };

    private static (AppDbContext Db, VehiclesController Controller, Customer Owner) Build()
    {
        var db = TestApi.NewDatabase();
        var owner = db.AddCustomer("Dono do carro");
        return (db, new VehiclesController(db).AsUser(), owner);
    }

    [Fact]
    public async Task CreateStoresThePlateNormalized()
    {
        var (db, controller, owner) = Build();

        var created = TestApi.Body(await controller.Create(Valid(owner.Id), default));

        Assert.Equal("ABC1D23", created.Plate);
        Assert.Equal(owner.Id, created.OwnerId);
        Assert.Equal("Dono do carro", created.OwnerName);
        Assert.Single(db.Vehicles);
    }

    // uq_vehicles_plate lives in the migration; this is the friendly 409 the
    // screen shows before the database ever gets the chance to complain.
    [Fact]
    public async Task ASecondVehicleWithTheSamePlateIsAConflict()
    {
        var (db, controller, owner) = Build();
        await controller.Create(Valid(owner.Id, "ABC1D23"), default);

        var result = await controller.Create(Valid(owner.Id, "abc 1d23"), default);

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Single(db.Vehicles);
    }

    [Fact]
    public async Task UpdateDoesNotConflictWithTheVehiclesOwnPlate()
    {
        var (_, controller, owner) = Build();
        var created = TestApi.Body(await controller.Create(Valid(owner.Id), default));

        var updated = TestApi.Body(await controller.Update(
            created.Id, Valid(owner.Id) with { Model = "Mobi" }, default));

        Assert.Equal("ABC1D23", updated.Plate);
        Assert.Equal("Mobi", updated.Model);
    }

    [Fact]
    public async Task UpdateToAPlateAlreadyTakenIsAConflict()
    {
        var (_, controller, owner) = Build();
        await controller.Create(Valid(owner.Id, "ABC1D23"), default);
        var second = TestApi.Body(await controller.Create(Valid(owner.Id, "XYZ4321"), default));

        var result = await controller.Update(second.Id, Valid(owner.Id, "ABC1D23"), default);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Theory]
    [InlineData("AB1234")]
    [InlineData("ABCD123")]
    [InlineData("")]
    public async Task AMalformedPlateIsRejected(string plate)
    {
        var (db, controller, owner) = Build();

        var result = await controller.Create(Valid(owner.Id, plate), default);

        result.Result.AssertRejects("Plate");
        Assert.Empty(db.Vehicles);
    }

    [Fact]
    public async Task AnUnknownOwnerIsRejected()
    {
        var (_, controller, _) = Build();

        var result = await controller.Create(Valid(Guid.NewGuid()), default);

        result.Result.AssertRejects("OwnerId");
    }

    [Fact]
    public async Task AnInactiveOwnerIsRejected()
    {
        var db = TestApi.NewDatabase();
        var owner = db.AddCustomer("Inativo", isActive: false);
        var controller = new VehiclesController(db).AsUser();

        var result = await controller.Create(Valid(owner.Id), default);

        result.Result.AssertRejects("OwnerId");
    }

    [Fact]
    public async Task AMalformedVinIsRejected()
    {
        var (_, controller, owner) = Build();

        var result = await controller.Create(
            Valid(owner.Id) with { Vin = "9BWZZZ377VT0042" }, default);

        result.Result.AssertRejects("Vin");
    }

    [Fact]
    public async Task AVinIsStoredUppercased()
    {
        var (_, controller, owner) = Build();

        var created = TestApi.Body(await controller.Create(
            Valid(owner.Id) with { Vin = "9bwzzz377vt004251" }, default));

        Assert.Equal("9BWZZZ377VT004251", created.Vin);
    }

    [Fact]
    public async Task AnImpossibleModelYearIsRejected()
    {
        var (_, controller, owner) = Build();

        var result = await controller.Create(
            Valid(owner.Id) with { ModelYear = 1780 }, default);

        result.Result.AssertRejects("ModelYear");
    }

    // Selling the car moves owner_id. The past service orders keep pointing at
    // the previous customer, which is exactly what D-08 buys.
    [Fact]
    public async Task UpdateTransfersTheVehicleToAnotherOwner()
    {
        var (db, controller, owner) = Build();
        var buyer = db.AddCustomer("Comprador");
        var created = TestApi.Body(await controller.Create(Valid(owner.Id), default));

        var updated = TestApi.Body(await controller.Update(
            created.Id, Valid(buyer.Id), default));

        Assert.Equal(buyer.Id, updated.OwnerId);
        Assert.Equal("Comprador", updated.OwnerName);
    }

    [Fact]
    public async Task ListFiltersByOwnerAndFindsByPlateWithoutTheDash()
    {
        var (db, controller, owner) = Build();
        var other = db.AddCustomer("Outro dono");
        await controller.Create(Valid(owner.Id, "ABC1D23"), default);
        await controller.Create(Valid(other.Id, "XYZ4321"), default);

        var mine = TestApi.Body(await controller.List(null, owner.Id, null, null, default));
        var byPlate = TestApi.Body(await controller.List("xyz-4321", null, null, null, default));

        Assert.Equal("ABC1D23", Assert.Single(mine.Items).Plate);
        Assert.Equal("XYZ4321", Assert.Single(byPlate.Items).Plate);
    }

    [Fact]
    public async Task DeleteRemovesAVehicleWithoutServiceOrders()
    {
        var (db, controller, owner) = Build();
        var created = TestApi.Body(await controller.Create(Valid(owner.Id), default));

        Assert.IsType<NoContentResult>(await controller.Delete(created.Id, default));
        Assert.Empty(db.Vehicles);
    }

    [Fact]
    public async Task DeleteOfAnUnknownIdIsNotFound()
    {
        var (_, controller, _) = Build();

        Assert.IsType<NotFoundObjectResult>(await controller.Delete(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task GetOfAnUnknownIdIsNotFound()
    {
        var (_, controller, _) = Build();

        var result = await controller.Get(Guid.NewGuid(), default);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }
}
