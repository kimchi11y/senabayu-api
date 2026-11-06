using System.ComponentModel.DataAnnotations;
using Homestay.Api.Domain;

namespace Homestay.Api.Dtos;

public record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);

public record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt);

public record AdminBookingDto(
    int Id,
    string Reference,
    string Status,
    string RoomTypeCode,
    string RoomNumber,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Nights,
    int GuestCount,
    string GuestName,
    string Email,
    string Phone,
    decimal Total,
    string Currency,
    DateTimeOffset CreatedAt)
{
    public static AdminBookingDto From(Booking booking) => new(
        booking.Id,
        booking.Reference,
        booking.Status.ToString(),
        booking.RoomType.Code,
        booking.Room.Number,
        booking.CheckIn,
        booking.CheckOut,
        booking.Nights,
        booking.GuestCount,
        booking.GuestName,
        booking.Email,
        booking.Phone,
        booking.TotalAmount,
        booking.Currency,
        booking.CreatedAt);
}

public record AdminBookingPageDto(List<AdminBookingDto> Items, int Total, int Page, int PageSize);
