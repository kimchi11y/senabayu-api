using Homestay.Api.Domain;

namespace Homestay.Tests;

public class BookingStatusTests
{
    [Theory]
    [InlineData(BookingStatus.Pending, BookingStatus.Confirmed, true)]
    [InlineData(BookingStatus.Pending, BookingStatus.Cancelled, true)]
    [InlineData(BookingStatus.Confirmed, BookingStatus.Cancelled, true)]
    [InlineData(BookingStatus.Confirmed, BookingStatus.Confirmed, false)]
    [InlineData(BookingStatus.Confirmed, BookingStatus.Pending, false)]
    [InlineData(BookingStatus.Cancelled, BookingStatus.Confirmed, false)]
    [InlineData(BookingStatus.Cancelled, BookingStatus.Pending, false)]
    [InlineData(BookingStatus.Cancelled, BookingStatus.Cancelled, false)]
    public void Only_allowed_status_changes_pass(BookingStatus from, BookingStatus to, bool allowed)
    {
        var booking = new Booking { Status = from };

        Assert.Equal(allowed, booking.CanChangeTo(to));
    }
}
