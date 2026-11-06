using Homestay.Api.Dtos;
using Homestay.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Homestay.Api.Controllers;

[ApiController]
[Route("api/v1/availability")]
public class AvailabilityController(BookingService bookings) : ControllerBase
{
    /// <summary>How many rooms of each type are free for the whole stay.</summary>
    [HttpGet]
    [ProducesResponseType<List<AvailabilityDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<AvailabilityDto>>> GetAvailability(
        [FromQuery, BindRequired] DateOnly checkIn,
        [FromQuery, BindRequired] DateOnly checkOut,
        CancellationToken cancellationToken)
    {
        var availability = await bookings.GetAvailabilityAsync(checkIn, checkOut, cancellationToken);

        return availability.Select(a => new AvailabilityDto(a.RoomTypeCode, a.AvailableRooms)).ToList();
    }
}
