using Microsoft.EntityFrameworkCore;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;

// The repository root .env is the single source of local configuration, as
// documented in README.md. TraversePath walks up from the project directory.
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "ConnectionStrings__Default is not set. Copy .env.example to .env at the repository root.");

// Postgres native enums. MapEnum both declares the type in the model, so the
// migration creates it, and maps the CLR property onto it. The [PgName]
// attributes keep the database labels uppercase.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
    {
        npgsql.MapEnum<UserRole>("user_role");
        npgsql.MapEnum<ServiceOrderStatus>("service_order_status");
        npgsql.MapEnum<ItemType>("item_type");
        npgsql.MapEnum<QuoteStatus>("quote_status");
        npgsql.MapEnum<MovementType>("movement_type");
    }).UseSnakeCaseNamingConvention());

builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// The generated OpenAPI document is the API contract (see docs/decisions.md, D-17).
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
