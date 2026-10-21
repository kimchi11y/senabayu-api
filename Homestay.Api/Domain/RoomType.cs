namespace Homestay.Api.Domain;

/// <summary>
/// A kind of room guests book (e.g. Family Room). Prices are per night in MYR.
/// </summary>
public class RoomType
{
    public int Id { get; set; }

    /// <summary>Stable identifier used in URLs, e.g. "FAMILY".</summary>
    public string Code { get; set; } = string.Empty;

    public string NameMs { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string DescriptionMs { get; set; } = string.Empty;
    public string DescriptionEn { get; set; } = string.Empty;

    public decimal BasePricePerNight { get; set; }
    public int MaxGuests { get; set; }
    public int MinNights { get; set; }

    /// <summary>Charged per night for each guest above two.</summary>
    public decimal ExtraGuestFeePerNight { get; set; }

    public bool IsActive { get; set; } = true;

    public List<Room> Rooms { get; set; } = [];
}
