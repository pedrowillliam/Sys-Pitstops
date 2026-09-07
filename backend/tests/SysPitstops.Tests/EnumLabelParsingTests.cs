using SysPitstops.Api.Domain;
using Xunit;

namespace SysPitstops.Tests;

/// <summary>The query string does not use the JSON converter, so the label has
/// to be parsed by hand. These lock the round trip: what ToPgName writes is
/// what TryParseLabel reads.</summary>
public class EnumLabelParsingTests
{
    [Theory]
    [InlineData("IN_YARD", ServiceOrderStatus.InYard)]
    [InlineData("AWAITING_APPROVAL", ServiceOrderStatus.AwaitingApproval)]
    [InlineData("REQUESTED", ServiceOrderStatus.Requested)]
    [InlineData("CANCELED", ServiceOrderStatus.Canceled)]
    public void ReadsTheDatabaseLabel(string label, ServiceOrderStatus expected)
    {
        Assert.True(EnumExtensions.TryParseLabel(typeof(ServiceOrderStatus), label, out var value));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void EveryStatusSurvivesTheRoundTrip()
    {
        foreach (var status in Enum.GetValues<ServiceOrderStatus>())
        {
            Assert.True(
                EnumExtensions.TryParseLabel(typeof(ServiceOrderStatus), status.ToPgName(), out var back),
                $"{status} não voltou de {status.ToPgName()}");
            Assert.Equal(status, back);
        }
    }

    [Theory]
    [InlineData("MECHANIC", UserRole.Mechanic)]
    [InlineData("ADMIN", UserRole.Admin)]
    public void WorksForAnyEnum(string label, UserRole expected)
    {
        Assert.True(EnumExtensions.TryParseLabel(typeof(UserRole), label, out var value));
        Assert.Equal(expected, value);
    }

    // The C# name is accepted because dropping underscores makes the two equal;
    // it is not a documented input, just a harmless consequence.
    [Fact]
    public void TheCSharpNameAlsoParses()
    {
        Assert.True(EnumExtensions.TryParseLabel(typeof(ServiceOrderStatus), "InYard", out var value));
        Assert.Equal(ServiceOrderStatus.InYard, value);
    }

    // The contract publishes labels. A "2" arriving is a caller using the old
    // integer format, and failing loudly is what surfaces it.
    [Theory]
    [InlineData("2")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("EM_ANDAMENTO")]
    [InlineData(null)]
    public void OrdinalsAndUnknownLabelsAreRefused(string? raw)
    {
        Assert.False(EnumExtensions.TryParseLabel(typeof(ServiceOrderStatus), raw, out var value));
        Assert.Null(value);
    }
}
