namespace Homestay.Api.Domain;

/// <summary>
/// A physical unit of a <see cref="RoomType"/>. Bookings are assigned to a specific room.
/// </summary>
public class Room
{
    public int Id { get; set; }

    public int RoomTypeId { get; set; }
    public RoomType RoomType { get; set; } = null!;

    /// <summary>Label on the door, e.g. "F01".</summary>
    public string Number { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
