using Homestay.Api.Dtos;
using Homestay.Api.Infrastructure;
using Homestay.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;

namespace Homestay.Api.Controllers;

[ApiController]
[Route("api/v1/bookings")]
public class BookingsController(BookingService bookings) : ControllerBase
{
    /// <summary>Creates a pending booking in the first free room of the chosen type.</summary>
    [HttpPost]
    [EnableRateLimiting(RateLimits.BookingWrites)]
    [ProducesResponseType<BookingDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<BookingDto>> CreateBooking(
        CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var booking = await bookings.CreateAsync(
            new NewBooking(
                request.RoomTypeCode,
                request.CheckIn,
                request.CheckOut,
                request.GuestCount,
                request.GuestName,
                request.Email,
                request.Phone),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, BookingDto.From(booking));
    }

    /// <summary>Looks up a booking by its reference and the email it was made with.</summary>
    [HttpGet("{reference}")]
    [EnableRateLimiting(RateLimits.BookingLookups)]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<BookingDto>> GetBooking(
        string reference, [FromQuery, BindRequired] string email, CancellationToken cancellationToken)
    {
        var booking = await bookings.FindAsync(reference, email, cancellationToken);

        return BookingDto.From(booking);
    }
}
