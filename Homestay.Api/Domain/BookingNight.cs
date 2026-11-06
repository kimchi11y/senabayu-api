namespace Homestay.Api.Domain;

public enum BookingNightStatus
{
    Active,
    Cancelled
}

/// <summary>
/// One night of one room. These rows exist so the database itself can refuse a double booking:
/// a unique index allows only one active row per room per night.
/// </summary>
public class BookingNight
{
    public int Id { get; set; }

    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public DateOnly NightDate { get; set; }

    public BookingNightStatus Status { get; set; } = BookingNightStatus.Active;
}
