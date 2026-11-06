using Homestay.Api.Domain;
using Homestay.Api.Services;

namespace Homestay.Tests;

public class PricingServiceTests
{
    private static readonly RoomType Family = new()
    {
        BasePricePerNight = 250m,
        ExtraGuestFeePerNight = 30m
    };

    private static readonly DateOnly CheckIn = new(2030, 1, 10);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Guests_within_the_included_two_pay_no_extra_fee(int guests)
    {
        var quote = PricingService.Calculate(Family, CheckIn, CheckIn.AddDays(3), guests);

        Assert.Equal(3, quote.Nights);
        Assert.Equal(0, quote.ExtraGuests);
        Assert.Equal(0m, quote.ExtraGuestFee);
        Assert.Equal(750m, quote.Total);
    }

    [Fact]
    public void Each_guest_above_two_pays_the_fee_every_night()
    {
        var quote = PricingService.Calculate(Family, CheckIn, CheckIn.AddDays(2), guestCount: 5);

        Assert.Equal(3, quote.ExtraGuests);
        Assert.Equal(180m, quote.ExtraGuestFee); // 2 nights x 3 guests x RM30
        Assert.Equal(680m, quote.Total); // 500 + 180
    }

    [Fact]
    public void Check_out_day_is_not_charged()
    {
        var quote = PricingService.Calculate(Family, CheckIn, CheckIn.AddDays(1), guestCount: 2);

        Assert.Equal(1, quote.Nights);
        Assert.Equal(250m, quote.Total);
    }
}
