using Homestay.Api.Domain;

namespace Homestay.Api.Services;

public record PriceQuote(int Nights, decimal PricePerNight, int ExtraGuests, decimal ExtraGuestFee, decimal Total);

/// <summary>The only place prices are calculated, so a quote and its booking can't disagree.</summary>
public static class PricingService
{
    /// <summary>Guests included in the nightly rate; each one above this pays the extra-guest fee.</summary>
    public const int IncludedGuests = 2;

    public static PriceQuote Calculate(RoomType roomType, DateOnly checkIn, DateOnly checkOut, int guestCount)
    {
        var nights = checkOut.DayNumber - checkIn.DayNumber;
        var extraGuests = Math.Max(0, guestCount - IncludedGuests);
        var extraGuestFee = nights * extraGuests * roomType.ExtraGuestFeePerNight;
        var total = nights * roomType.BasePricePerNight + extraGuestFee;

        return new PriceQuote(nights, roomType.BasePricePerNight, extraGuests, extraGuestFee, total);
    }
}
