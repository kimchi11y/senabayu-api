using System.ComponentModel.DataAnnotations;
using Homestay.Api.Domain;
using Homestay.Api.Services;

namespace Homestay.Api.Dtos;

public record AvailabilityDto(string RoomTypeCode, int AvailableRooms);

public record QuoteRequest(
    [Required] string RoomTypeCode,
    DateOnly CheckIn,
    DateOnly CheckOut,
    [Range(1, 50)] int GuestCount);

public record QuoteDto(
    int Nights,
    decimal PricePerNight,
    int ExtraGuests,
    decimal ExtraGuestFee,
    decimal Total,
    string Currency)
{
    public static QuoteDto From(PriceQuote quote) => new(
        quote.Nights, quote.PricePerNight, quote.ExtraGuests, quote.ExtraGuestFee, quote.Total, "MYR");
}

public record CreateBookingRequest(
    [Required] string RoomTypeCode,
    DateOnly CheckIn,
    DateOnly CheckOut,
    [Range(1, 50)] int GuestCount,
    [Required, StringLength(100, MinimumLength = 2)] string GuestName,
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(32, MinimumLength = 7)] string Phone);

public record BookingDto(
    string Reference,
    string Status,
    string RoomTypeCode,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Nights,
    int GuestCount,
    string GuestName,
    decimal PricePerNight,
    decimal ExtraGuestFee,
    decimal Total,
    string Currency)
{
    public static BookingDto From(Booking booking) => new(
        booking.Reference,
        booking.Status.ToString(),
        booking.RoomType.Code,
        booking.CheckIn,
        booking.CheckOut,
        booking.Nights,
        booking.GuestCount,
        booking.GuestName,
        booking.PricePerNightSnapshot,
        booking.ExtraGuestFee,
        booking.TotalAmount,
        booking.Currency);
}
