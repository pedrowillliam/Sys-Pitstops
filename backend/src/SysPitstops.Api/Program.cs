using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SysPitstops.Api.Auth;
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

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<TokenService>();

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

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

var app = builder.Build();

await AdminSeeder.SeedAsync(app.Services);

// The generated OpenAPI document is the API contract (see docs/decisions.md, D-17).
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
