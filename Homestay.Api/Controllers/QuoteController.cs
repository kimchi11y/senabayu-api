using Homestay.Api.Dtos;
using Homestay.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Homestay.Api.Controllers;

[ApiController]
[Route("api/v1/quote")]
public class QuoteController(BookingService bookings) : ControllerBase
{
    /// <summary>Prices a stay without booking it.</summary>
    [HttpPost]
    [ProducesResponseType<QuoteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuoteDto>> GetQuote(QuoteRequest request, CancellationToken cancellationToken)
    {
        var quote = await bookings.QuoteAsync(
            request.RoomTypeCode, request.CheckIn, request.CheckOut, request.GuestCount, cancellationToken);

        return QuoteDto.From(quote);
    }
}
