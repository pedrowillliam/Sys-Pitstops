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

    public static TController AsUser<TController>(
        this TController controller, string role = Roles.Admin) where TController : ControllerBase
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(TokenService.SubjectClaim, Guid.NewGuid().ToString()),
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
