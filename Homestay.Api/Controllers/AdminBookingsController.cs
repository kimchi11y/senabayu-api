using Homestay.Api.Domain;
using Homestay.Api.Dtos;
using Homestay.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homestay.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = Roles.Admin)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class AdminBookingsController(BookingService bookings) : ControllerBase
{
    private const int PageSize = 20;

    /// <summary>Lists bookings, newest first.</summary>
    [HttpGet("bookings")]
    [ProducesResponseType<AdminBookingPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminBookingPageDto>> GetBookings(
        [FromQuery] BookingStatus? status,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        var currentPage = Math.Max(1, page);
        var (items, total) = await bookings.ListAsync(status, currentPage, PageSize, cancellationToken);

        return new AdminBookingPageDto(items.Select(AdminBookingDto.From).ToList(), total, currentPage, PageSize);
    }

    /// <summary>Confirms a pending booking.</summary>
    [HttpPost("bookings/{id:int}/confirm")]
    [ProducesResponseType<AdminBookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminBookingDto>> Confirm(int id, CancellationToken cancellationToken) =>
        AdminBookingDto.From(await bookings.ConfirmAsync(id, cancellationToken));

    /// <summary>Cancels a booking and frees its room for those nights.</summary>
    [HttpPost("bookings/{id:int}/cancel")]
    [ProducesResponseType<AdminBookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminBookingDto>> Cancel(int id, CancellationToken cancellationToken) =>
        AdminBookingDto.From(await bookings.CancelAsync(id, cancellationToken));

    /// <summary>Deletes every booking, so the demo starts clean.</summary>
    [HttpPost("demo/reset")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetDemo(CancellationToken cancellationToken)
    {
        await bookings.DeleteAllAsync(cancellationToken);
        return NoContent();
    }
}
