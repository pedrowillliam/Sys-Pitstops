using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using SysPitstops.Api.Auth;
using SysPitstops.Api.Domain;
using Xunit;

namespace SysPitstops.Tests;

public class PasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();

    [Fact]
    public void VerifyAcceptsTheOriginalPassword()
    {
        var hash = _hasher.Hash("senha-do-mecanico");
        Assert.True(_hasher.Verify("senha-do-mecanico", hash));
    }

    [Fact]
    public void VerifyRejectsAWrongPassword()
    {
        var hash = _hasher.Hash("senha-do-mecanico");
        Assert.False(_hasher.Verify("senha-errada", hash));
    }

    [Fact]
    public void HashIsSaltedSoEqualPasswordsProduceDifferentHashes()
    {
        Assert.NotEqual(_hasher.Hash("mesma-senha"), _hasher.Hash("mesma-senha"));
    }

    [Fact]
    public void VerifyTreatsAMalformedHashAsAFailedLogin()
    {
       
        Assert.False(_hasher.Verify("qualquer", "isto-nao-e-um-hash-bcrypt"));
    }
}

public class TokenServiceTests
{
    private static readonly User Mechanic = new()
    {
        Id = Guid.NewGuid(),
        WorkshopId = 1,
        Name = "Roberto Silva",
        Email = "roberto@oficina.local",
        PasswordHash = "irrelevante",
        Role = UserRole.Mechanic
    };

    private static TokenService Build() => new(Options.Create(new JwtOptions
    {
        Issuer = "syspitstops",
        Audience = "syspitstops",
        Secret = "segredo-de-teste-com-mais-de-32-bytes-aqui"
    }));

    [Fact]
    public void TokenCarriesSubjectRoleAndWorkshop()
    {
        var (token, _) = Build().Create(Mechanic);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(Mechanic.Id.ToString(), jwt.Claims.Single(c => c.Type == TokenService.SubjectClaim).Value);
        Assert.Equal("MECHANIC", jwt.Claims.Single(c => c.Type == TokenService.RoleClaim).Value);
        Assert.Equal("1", jwt.Claims.Single(c => c.Type == TokenService.WorkshopClaim).Value);
    }

    [Fact]
    public void TokenLastsEightHours()
    {

        var (_, expiresAt) = Build().Create(Mechanic);
        var hours = (expiresAt - DateTimeOffset.UtcNow).TotalHours;

        Assert.InRange(hours, 7.9, 8.0);
    }

    [Fact]
    public void TokenIsIssuedForTheConfiguredIssuerAndAudience()
    {
        var (token, _) = Build().Create(Mechanic);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("syspitstops", jwt.Issuer);
        Assert.Contains("syspitstops", jwt.Audiences);
    }
}

public class EnumLabelTests
{
    [Theory]
    [InlineData(UserRole.Admin, "ADMIN")]
    [InlineData(UserRole.Attendant, "ATTENDANT")]
    [InlineData(UserRole.Mechanic, "MECHANIC")]
    public void RoleUsesTheDatabaseLabel(UserRole role, string expected)
    {
        Assert.Equal(expected, role.ToPgName());
    }

    [Theory]
    [InlineData(ServiceOrderStatus.InYard, "IN_YARD")]
    [InlineData(ServiceOrderStatus.AwaitingApproval, "AWAITING_APPROVAL")]
    [InlineData(ServiceOrderStatus.InProgress, "IN_PROGRESS")]
    public void StatusKeepsTheUnderscoredLabel(ServiceOrderStatus status, string expected)
    {
        Assert.Equal(expected, status.ToPgName());
    }
}
