using System.Globalization;
using PcMarket.Application.Validation;
using PcMarket.Contracts.Common;
using PcMarket.Contracts.Orders;

namespace PcMarket.UnitTests.Bot;

/// <summary>The delivery pin a bot order is built around: the links a courier taps, and the rule that lets an
/// order identify its destination either in writing or on a map.</summary>
public class DeliveryLocationTests
{
    private const double Latitude = 41.311081;
    private const double Longitude = 69.240562;

    /// <summary>Russian and Uzbek both write decimals with a comma, which in a URL would either truncate the
    /// coordinate or split it into two query values - either way pointing the courier somewhere else. The
    /// links must therefore ignore the ambient culture. (A comma-separator culture is built by hand rather
    /// than named, because the test project runs in globalization-invariant mode.)</summary>
    [Fact]
    public void MapLinks_FormatCoordinatesInvariantly()
    {
        var commaDecimal = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        commaDecimal.NumberFormat.NumberDecimalSeparator = ",";

        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = commaDecimal;

            Assert.Contains("41.311081,69.240562", MapLinks.Google(Latitude, Longitude));
            Assert.Contains("69.240562,41.311081", MapLinks.Yandex(Latitude, Longitude));
            Assert.Equal("41.311081, 69.240562", MapLinks.Coordinates(Latitude, Longitude));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>Yandex takes its point as longitude-first, which is the opposite of Google — getting this
    /// backwards silently points at a different country.</summary>
    [Fact]
    public void MapLinks_Yandex_PutsLongitudeFirst() =>
        Assert.Contains($"pt={Longitude.ToString(CultureInfo.InvariantCulture)},", MapLinks.Yandex(Latitude, Longitude));

    [Fact]
    public void Validator_AcceptsABotOrder_WithAPinAndNoRegionOrCity() =>
        Assert.True(Validate(new ShippingAddressDto("", "", "12, flat 5", null, Latitude, Longitude)).IsValid);

    [Fact]
    public void Validator_AcceptsAWebOrder_WithAWrittenAddressAndNoPin() =>
        Assert.True(Validate(new ShippingAddressDto("Toshkent shahri", "Chilonzor", "Amir Temur 1", null)).IsValid);

    [Fact]
    public void Validator_RejectsAnAddressThatSaysNeither() =>
        Assert.False(Validate(new ShippingAddressDto("", "", "12, flat 5", null)).IsValid);

    [Fact]
    public void Validator_RejectsAHalfPin() =>
        Assert.False(Validate(new ShippingAddressDto("", "", "12, flat 5", null, Latitude, null)).IsValid);

    [Theory]
    [InlineData(91d, 69.24)]
    [InlineData(41.31, 181d)]
    public void Validator_RejectsCoordinatesOffTheGlobe(double latitude, double longitude) =>
        Assert.False(Validate(new ShippingAddressDto("", "", "12, flat 5", null, latitude, longitude)).IsValid);

    private static FluentValidation.Results.ValidationResult Validate(ShippingAddressDto address) =>
        new CreateOrderRequestValidator().Validate(
            new CreateOrderRequest(PaymentMethod.Cash, DeliveryType.Courier, AddressId: null, address));
}
