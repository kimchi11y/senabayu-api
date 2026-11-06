namespace Homestay.Api.Domain;

public enum BookingStatus
{
    Pending,
    Confirmed,
    Cancelled
}

/// <summary>
/// A guest's stay in one room. CheckOut is exclusive: the guest doesn't sleep there that night.
/// Prices are copied in at booking time, so later rate changes don't alter existing bookings.
/// </summary>
public class Booking
{
    public int Id { get; set; }

    /// <summary>What the guest quotes when looking the booking up, e.g. "SBY-7KQ2MX".</summary>
    public string Reference { get; set; } = string.Empty;

    public int RoomTypeId { get; set; }
    public RoomType RoomType { get; set; } = null!;

    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public DateOnly CheckIn { get; set; }
    public DateOnly CheckOut { get; set; }

    public string GuestName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int GuestCount { get; set; }

    public int Nights { get; set; }
    public decimal PricePerNightSnapshot { get; set; }

    /// <summary>Total extra-guest charge for the whole stay.</summary>
    public decimal ExtraGuestFee { get; set; }

    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "MYR";

    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; }
}
