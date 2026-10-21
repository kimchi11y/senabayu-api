using Homestay.Api.Data;
using Homestay.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Homestay.Api.Controllers;

[ApiController]
[Route("api/v1/room-types")]
public class RoomTypesController(AppDbContext db) : ControllerBase
{
    private static readonly string[] SupportedLocales = ["ms", "en"];

    /// <summary>Lists active room types, cheapest first.</summary>
    /// <param name="locale">"ms" or "en" (default).</param>
    [HttpGet]
    [ProducesResponseType<List<RoomTypeDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<RoomTypeDto>>> GetRoomTypes(
        [FromQuery] string locale = "en",
        CancellationToken cancellationToken = default)
    {
        if (!SupportedLocales.Contains(locale))
        {
            ModelState.AddModelError(nameof(locale), "Locale must be 'ms' or 'en'.");
            return ValidationProblem(ModelState);
        }

        var isMs = locale == "ms";

        var roomTypes = await db.RoomTypes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.BasePricePerNight)
            .Select(t => new RoomTypeDto(
                t.Code,
                isMs ? t.NameMs : t.NameEn,
                isMs ? t.DescriptionMs : t.DescriptionEn,
                t.BasePricePerNight,
                t.ExtraGuestFeePerNight,
                t.MaxGuests,
                t.MinNights))
            .ToListAsync(cancellationToken);

        return roomTypes;
    }
}
