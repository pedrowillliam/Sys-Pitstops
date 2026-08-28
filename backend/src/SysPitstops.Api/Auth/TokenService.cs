using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SysPitstops.Api.Domain;

namespace SysPitstops.Api.Auth;

public class JwtOptions
{
    public const string SectionName = "JWT";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
}

public class TokenService(IOptions<JwtOptions> options)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);

    public const string SubjectClaim = "sub";
    public const string RoleClaim = "role";
    public const string WorkshopClaim = "workshop";

    private readonly JwtOptions _options = options.Value;

    public (string Token, DateTimeOffset ExpiresAt) Create(User user)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(Lifetime);

        var claims = new List<Claim>
        {
            new(SubjectClaim, user.Id.ToString()),
            new(RoleClaim, user.Role.ToPgName()),
            new(WorkshopClaim, user.WorkshopId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.Name),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
