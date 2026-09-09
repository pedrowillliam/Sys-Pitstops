using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using SysPitstops.Api.Auth;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;

namespace SysPitstops.Tests;

// The CRUD tests of D-24 run against the in-memory provider: they exercise the
// controller rules — normalization, validation and the plate conflict — not the
// Postgres constraints, which the migration already declares.
internal static class TestApi
{
    public const int WorkshopId = 1;

    public static AppDbContext NewDatabase()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"syspitstops-{Guid.NewGuid()}")
            .Options);

        db.Workshops.Add(new Workshop
        {
            Id = WorkshopId,
            Name = "Oficina de teste",
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.SaveChanges();

        return db;
    }

    public static Customer AddCustomer(this AppDbContext db, string name = "Cliente", bool isActive = true)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            WorkshopId = WorkshopId,
            Name = name,
            Phone = "81999990000",
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Customers.Add(customer);
        db.SaveChanges();

        return customer;
    }

    public static User AddUser(
        this AppDbContext db, string name = "Usuário", UserRole role = UserRole.Admin)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            WorkshopId = WorkshopId,
            Name = name,
            Email = $"{Guid.NewGuid():N}@teste",
            PasswordHash = "hash",
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Users.Add(user);
        db.SaveChanges();

        return user;
    }

    public static Vehicle AddVehicle(
        this AppDbContext db,
        Customer owner,
        string plate = "ABC1D23",
        string brand = "Fiat",
        string model = "Uno",
        int? modelYear = null)
    {
        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            WorkshopId = WorkshopId,
            OwnerId = owner.Id,
            Plate = plate,
            Brand = brand,
            Model = model,
            ModelYear = modelYear,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Vehicles.Add(vehicle);
        db.SaveChanges();

        return vehicle;
    }

    public static Part AddPart(
        this AppDbContext db,
        string name = "Peça",
        decimal salePrice = 100m,
        decimal quantityOnHand = 0m,
        decimal minQuantity = 0m,
        decimal costPrice = 0m,
        string? sku = null,
        bool isActive = true)
    {
        var part = new Part
        {
            Id = Guid.NewGuid(),
            WorkshopId = WorkshopId,
            Sku = sku ?? $"SKU-{Random.Shared.Next(100000, 999999)}",
            Name = name,
            SalePrice = salePrice,
            CostPrice = costPrice,
            QuantityOnHand = quantityOnHand,
            MinQuantity = minQuantity,
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Parts.Add(part);
        db.SaveChanges();

        return part;
    }

    public static ServiceOrder AddServiceOrder(
        this AppDbContext db,
        ServiceOrderStatus status = ServiceOrderStatus.Requested,
        User? mechanic = null,
        decimal discount = 0m)
    {
        var owner = db.AddCustomer();
        var vehicle = db.AddVehicle(owner, $"P{Random.Shared.Next(100000, 999999)}");
        var author = db.AddUser();
        var now = DateTimeOffset.UtcNow;

        var order = new ServiceOrder
        {
            Id = Guid.NewGuid(),
            WorkshopId = WorkshopId,
            VehicleId = vehicle.Id,
            CustomerId = owner.Id,
            MechanicId = mechanic?.Id,
            CreatedBy = author.Id,
            Status = status,
            DiscountAmount = discount,
            OpenedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.ServiceOrders.Add(order);
        db.SaveChanges();

        return order;
    }

    // A null part makes it a service item — the common case. Passing one makes
    // it a PART item, which is what the stock write-off of D-11 reads.
    public static ServiceOrderItem AddItem(
        this AppDbContext db,
        ServiceOrder order,
        string description,
        decimal quantity,
        decimal unitPrice,
        Part? part = null)
    {
        var item = new ServiceOrderItem
        {
            Id = Guid.NewGuid(),
            ServiceOrderId = order.Id,
            ItemType = part is null ? ItemType.Service : ItemType.Part,
            PartId = part?.Id,
            Description = description,
            Quantity = quantity,
            UnitPrice = unitPrice,
            CreatedBy = order.CreatedBy,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.ServiceOrderItems.Add(item);
        db.SaveChanges();

        return item;
    }

    public static TController AsUser<TController>(
        this TController controller, User user) where TController : ControllerBase =>
        controller.AsUser(user.Role switch
        {
            UserRole.Attendant => Roles.Attendant,
            UserRole.Mechanic => Roles.Mechanic,
            _ => Roles.Admin
        }, user.Id);

    public static TController AsUser<TController>(
        this TController controller, string role = Roles.Admin, Guid? userId = null)
        where TController : ControllerBase
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(TokenService.SubjectClaim, (userId ?? Guid.NewGuid()).ToString()),
            new Claim(TokenService.RoleClaim, role),
            new Claim(TokenService.WorkshopClaim, WorkshopId.ToString())
        ], "Test");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        controller.ProblemDetailsFactory = new TestProblemDetailsFactory();

        return controller;
    }

    public static T Body<T>(ActionResult<T> result) where T : class => result.Result switch
    {
        OkObjectResult ok => (T)ok.Value!,
        CreatedAtActionResult created => (T)created.Value!,
        _ => throw new InvalidOperationException($"Resposta inesperada: {result.Result?.GetType().Name}")
    };
}

// ControllerBase.ValidationProblem goes through this factory, which normally
// comes from the request services. The tests build controllers by hand.
internal sealed class TestProblemDetailsFactory : ProblemDetailsFactory
{
    public override ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        int? statusCode = null,
        string? title = null,
        string? type = null,
        string? detail = null,
        string? instance = null) => new()
        {
            Status = statusCode ?? StatusCodes.Status500InternalServerError,
            Title = title,
            Type = type,
            Detail = detail,
            Instance = instance
        };

    public override ValidationProblemDetails CreateValidationProblemDetails(
        HttpContext httpContext,
        ModelStateDictionary modelStateDictionary,
        int? statusCode = null,
        string? title = null,
        string? type = null,
        string? detail = null,
        string? instance = null) => new(modelStateDictionary)
        {
            Status = statusCode ?? StatusCodes.Status400BadRequest,
            Title = title,
            Type = type,
            Detail = detail,
            Instance = instance
        };
}
