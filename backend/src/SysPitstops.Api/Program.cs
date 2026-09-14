using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SysPitstops.Api.Auth;
using SysPitstops.Api.Contracts;
using SysPitstops.Api.Controllers;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;
using SysPitstops.Api.Seeding;
using SysPitstops.Api.Storage;

// The repository root .env is the single source of local configuration, as
// documented in README.md. There is no .env in the published container — the
// values come from the environment itself, so a missing file is not an error.
//
//   onlyExactPath: false     walk up from the project directory to find it
//   clobberExistingVars: false   a stray .env never beats what the host set
DotNetEnv.Env.Load(options: new DotNetEnv.LoadOptions(
    clobberExistingVars: false, onlyExactPath: false));

var builder = WebApplication.CreateBuilder(args);

// Render assigns the port at run time and expects the process to bind it.
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
{
    builder.WebHost.UseUrls($"http://+:{port}");
}

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

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<TokenService>();

// D-25: the upload talks to the interface, never to a folder. Swapping the
// destination for the Supabase bucket of D-26 is a new class and this line.
builder.Services.Configure<MediaOptions>(
    builder.Configuration.GetSection(MediaOptions.SectionName));
builder.Services.AddSingleton<IMediaStorage, LocalDiskMediaStorage>();

var jwtSecret = builder.Configuration[$"{JwtOptions.SectionName}:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || Encoding.UTF8.GetByteCount(jwtSecret) < 32)
{
    throw new InvalidOperationException(
        "JWT__Secret must be set and have at least 32 bytes for HMAC-SHA256.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration[$"{JwtOptions.SectionName}:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration[$"{JwtOptions.SectionName}:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = TokenService.RoleClaim,
            NameClaimType = TokenService.SubjectClaim
        };

        options.Events = new JwtBearerEvents
        {
    
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue(AuthCookie.Name, out var token))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            },

            OnTokenValidated = async context =>
            {
                var subject = context.Principal?.FindFirst(TokenService.SubjectClaim)?.Value;
                if (!Guid.TryParse(subject, out var userId))
                {
                    context.Fail("Token without a valid subject.");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                if (!await db.Users.AnyAsync(u => u.Id == userId && u.IsActive))
                {
                    context.Fail("User is inactive or no longer exists.");
                }
            }
        };
    });

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(PublicQuotesController.RateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1)
            }));
});

builder.Services.AddAuthorization();
builder.Services.AddControllers(options =>
{
    // Same labels in the query string as in the body — see the binder's remarks.
    options.ModelBinderProviders.Insert(0, new LabelEnumModelBinderProvider());
}).AddJsonOptions(options =>
{
    // Enums travel as their database label — IN_YARD, not 4. Without this the
    // wire format would silently change whenever a member is reordered, and
    // the front would need a second copy of the labels. SnakeCaseUpper matches
    // the [PgName] values exactly, so JSON, Postgres and the UI agree.
    options.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
});
builder.Services.AddSwaggerGen(options =>
{
    // Without this every property comes out optional and nullable, and the
    // TypeScript generated from it (D-17) would force a null check on fields
    // the C# contract already guarantees.
    options.SupportNonNullableReferenceTypes();
    options.NonNullableReferenceTypesAsRequired();
});

// Render terminates TLS at its proxy and forwards plain HTTP to the container.
// Without this the app believes every request is http: UseHttpsRedirection
// would bounce forever and the Secure cookie of D-20 would never be set.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;
    // The proxy address is not knowable ahead of time on a PaaS.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

// The schema only ever changes through an EF Core migration (see CLAUDE.md),
// and this is the same migration the team runs locally — applied on startup so
// a deploy never leaves the container running against an older schema.
// See docs/decisions.md, D-28.
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

await AdminSeeder.SeedAsync(app.Services);
await DemoSeeder.SeedAsync(app.Services);

// The generated OpenAPI document is the API contract (see docs/decisions.md, D-17).
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// D-26: one service serves the API and the compiled SPA, so that front and API
// share an origin and the SameSite=Lax cookie of D-20 keeps working. The files
// only exist in the published image; in development the Vite proxy does this.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Render polls this to decide whether the deploy is live. It answers before
// authentication on purpose — a health check that needs a login is useless.
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

// Anything that is not an API route is a client-side route: hand back the
// shell and let React Router resolve it. Without this, reloading /customers
// in the browser would 404 in production but work in development.
app.MapFallbackToFile("index.html");

app.Run();
