using SysPitstops.Api.Domain;
using Xunit;

namespace SysPitstops.Tests;

public class PlateNumberTests
{
    [Theory]
    [InlineData("abc-1234", "ABC1234")]
    [InlineData("abc 1d23", "ABC1D23")]
    [InlineData("ABC1D23", "ABC1D23")]
    public void NormalizeStripsSeparatorsAndUppercases(string input, string expected) =>
        Assert.Equal(expected, PlateNumber.Normalize(input));

    [Theory]
    [InlineData("ABC1234")] // old layout
    [InlineData("ABC1D23")] // Mercosul
    public void BothBrazilianLayoutsAreValid(string plate) =>
        Assert.True(PlateNumber.IsValid(plate));

    [Theory]
    [InlineData("")]
    [InlineData("AB1234")]
    [InlineData("ABCD123")]
    [InlineData("ABC12345")]
    [InlineData("1BC1234")]
    public void MalformedPlatesAreRejected(string plate) =>
        Assert.False(PlateNumber.IsValid(plate));

    [Fact]
    public void NormalizationIsWhatMakesTheSamePlateCollide() =>
        Assert.Equal(PlateNumber.Normalize("abc-1d23"), PlateNumber.Normalize("ABC1D23"));
}

public class PhoneNumberTests
{
    [Theory]
    [InlineData("(81) 99999-0000", "81999990000")]
    [InlineData("81 3333-4444", "8133334444")]
    public void NormalizeKeepsOnlyDigits(string input, string expected) =>
        Assert.Equal(expected, PhoneNumber.Normalize(input));

    [Theory]
    [InlineData("81999990000")] // mobile
    [InlineData("8133334444")]  // landline
    public void TenAndElevenDigitsAreValid(string phone) =>
        Assert.True(PhoneNumber.IsValid(phone));

    [Theory]
    [InlineData("")]
    [InlineData("999990000")]
    [InlineData("819999900001")]
    [InlineData("01999990000")]
    public void OtherLengthsAreRejected(string phone) =>
        Assert.False(PhoneNumber.IsValid(phone));

    [Theory]
    [InlineData("81999990000")]
    [InlineData("11987654321")]
    public void ElevenDigitsWithTheNinthDigitIsAMobile(string phone) =>
        Assert.True(PhoneNumber.IsMobile(phone));

    /// <summary>A landline stays a valid customer phone; it just cannot receive
    /// the quote by WhatsApp.</summary>
    [Theory]
    [InlineData("8133334444")]
    [InlineData("81833334444")]
    [InlineData("")]
    public void ALandlineIsValidButIsNotAMobile(string phone)
    {
        Assert.False(PhoneNumber.IsMobile(phone));
        Assert.Equal(phone.Length is 10 or 11, PhoneNumber.IsValid(phone));
    }

    [Theory]
    [InlineData("5581999990000", "81999990000")]
    [InlineData("558133334444", "8133334444")]
    public void APastedCountryCodeIsDropped(string input, string expected) =>
        Assert.Equal(expected, PhoneNumber.StripCountryCode(input));

    /// <summary>The number itself may start with 55: only a length that says
    /// the country code is really there makes it go.</summary>
    [Theory]
    [InlineData("5599990000")]
    [InlineData("55999990000")]
    [InlineData("55")]
    public void AShorterNumberStartingWith55IsKept(string input) =>
        Assert.Equal(input, PhoneNumber.StripCountryCode(input));
}

public class TaxDocumentTests
{
    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("111.444.777-35")]
    public void ValidCpfIsAccepted(string document) =>
        Assert.True(TaxDocument.IsValid(TaxDocument.Normalize(document)));

    [Theory]
    [InlineData("11.222.333/0001-81")]
    public void ValidCnpjIsAccepted(string document) =>
        Assert.True(TaxDocument.IsValid(TaxDocument.Normalize(document)));

    [Theory]
    [InlineData("529.982.247-26")] // wrong check digit
    [InlineData("111.111.111-11")] // repeated digits
    [InlineData("11.222.333/0001-82")]
    [InlineData("123")]
    [InlineData("")]
    public void InvalidDocumentIsRejected(string document) =>
        Assert.False(TaxDocument.IsValid(TaxDocument.Normalize(document)));
}

public class VehicleIdentificationNumberTests
{
    [Fact]
    public void SeventeenAllowedCharactersAreValid() =>
        Assert.True(VehicleIdentificationNumber.IsValid(
            VehicleIdentificationNumber.Normalize("9bwzzz377vt004251")));

    [Theory]
    [InlineData("9BWZZZ377VT00425")]   // 16
    [InlineData("9BWZZZ377VT0042511")] // 18
    [InlineData("9BWZZZ377VT00425I")]  // I is not allowed
    [InlineData("9BWZZZ377VT00425O")]
    [InlineData("9BWZZZ377VT00425Q")]
    public void MalformedVinIsRejected(string vin) =>
        Assert.False(VehicleIdentificationNumber.IsValid(
            VehicleIdentificationNumber.Normalize(vin)));
}
