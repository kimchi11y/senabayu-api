namespace Homestay.Api.Dtos;

/// <summary>
/// A room type as the API returns it: name and description already in the requested locale.
/// </summary>
public record RoomTypeDto(
    string Code,
    string Name,
    string Description,
    decimal BasePricePerNight,
    decimal ExtraGuestFeePerNight,
    int MaxGuests,
    int MinNights);
