using System.Security.Cryptography;
using Homestay.Api.Data;
using Homestay.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Homestay.Api.Services;

public record RoomAvailability(string RoomTypeCode, int AvailableRooms);

public record NewBooking(
    string RoomTypeCode,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int GuestCount,
    string GuestName,
    string Email,
    string Phone);

public class BookingService(AppDbContext db, TimeProvider clock)
{
    private const int MaxNights = 30;

    // No 0/O or 1/I, so a reference read over the phone can't be misheard.
    private const string ReferenceAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    // Malaysia is UTC+8 all year, and "today" for a check-in means today at the homestay.
    private static readonly TimeSpan MalaysiaOffset = TimeSpan.FromHours(8);

    public async Task<List<RoomAvailability>> GetAvailabilityAsync(
        DateOnly checkIn, DateOnly checkOut, CancellationToken cancellationToken)
    {
        ValidateDates(checkIn, checkOut);

        return await db.RoomTypes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.BasePricePerNight)
            .Select(t => new RoomAvailability(
                t.Code,
                t.Rooms.Count(r => r.IsActive && !db.Bookings.Any(b =>
                    b.RoomId == r.Id
                    && b.Status != BookingStatus.Cancelled
                    && b.CheckIn < checkOut
                    && b.CheckOut > checkIn))))
            .ToListAsync(cancellationToken);
    }

    public async Task<PriceQuote> QuoteAsync(
        string roomTypeCode, DateOnly checkIn, DateOnly checkOut, int guestCount, CancellationToken cancellationToken)
    {
        var roomType = await FindRoomTypeAsync(roomTypeCode, cancellationToken);
        ValidateStay(roomType, checkIn, checkOut, guestCount);

        return PricingService.Calculate(roomType, checkIn, checkOut, guestCount);
    }

    /// <summary>
    /// Deliberately naive: it checks for a free room, then inserts. Two simultaneous requests can both pass the check
    /// and double-book. Milestone 3 proves this with a test, then fixes it with a database constraint.
    /// </summary>
    public async Task<Booking> CreateAsync(NewBooking request, CancellationToken cancellationToken)
    {
        var roomType = await FindRoomTypeAsync(request.RoomTypeCode, cancellationToken);
        ValidateStay(roomType, request.CheckIn, request.CheckOut, request.GuestCount);

        var roomId = await db.Rooms
            .Where(r => r.RoomTypeId == roomType.Id && r.IsActive)
            .Where(r => !db.Bookings.Any(b =>
                b.RoomId == r.Id
                && b.Status != BookingStatus.Cancelled
                && b.CheckIn < request.CheckOut
                && b.CheckOut > request.CheckIn))
            .OrderBy(r => r.Number)
            .Select(r => (int?)r.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BookingException(
                StatusCodes.Status409Conflict, "room_unavailable", "No room of this type is free for those dates.");

        var quote = PricingService.Calculate(roomType, request.CheckIn, request.CheckOut, request.GuestCount);

        var booking = new Booking
        {
            Reference = GenerateReference(),
            RoomTypeId = roomType.Id,
            RoomType = roomType,
            RoomId = roomId,
            CheckIn = request.CheckIn,
            CheckOut = request.CheckOut,
            GuestName = request.GuestName.Trim(),
            Email = NormalizeEmail(request.Email),
            Phone = request.Phone.Trim(),
            GuestCount = request.GuestCount,
            Nights = quote.Nights,
            PricePerNightSnapshot = quote.PricePerNight,
            ExtraGuestFee = quote.ExtraGuestFee,
            TotalAmount = quote.Total,
            CreatedAt = clock.GetUtcNow()
        };

        db.Bookings.Add(booking);
        await db.SaveChangesAsync(cancellationToken);

        return booking;
    }

    /// <summary>Needs both values, so nobody can browse other guests' bookings by guessing references.</summary>
    public async Task<Booking> FindAsync(string reference, string email, CancellationToken cancellationToken)
    {
        var normalizedReference = reference.Trim().ToUpperInvariant();
        var normalizedEmail = NormalizeEmail(email);

        return await db.Bookings
                   .AsNoTracking()
                   .Include(b => b.RoomType)
                   .FirstOrDefaultAsync(
                       b => b.Reference == normalizedReference && b.Email == normalizedEmail, cancellationToken)
               ?? throw new BookingException(
                   StatusCodes.Status404NotFound, "booking_not_found", "No booking matches that reference and email.");
    }

    private async Task<RoomType> FindRoomTypeAsync(string code, CancellationToken cancellationToken) =>
        await db.RoomTypes.FirstOrDefaultAsync(t => t.Code == code && t.IsActive, cancellationToken)
        ?? throw new BookingException(
            StatusCodes.Status404NotFound, "room_type_not_found", $"Room type '{code}' doesn't exist.");

    private void ValidateDates(DateOnly checkIn, DateOnly checkOut)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(MalaysiaOffset).DateTime);

        if (checkIn < today)
            throw InvalidStay("Check-in can't be in the past.");
        if (checkOut <= checkIn)
            throw InvalidStay("Check-out must be after check-in.");
        if (checkOut.DayNumber - checkIn.DayNumber > MaxNights)
            throw InvalidStay($"Stays can't be longer than {MaxNights} nights.");
    }

    private void ValidateStay(RoomType roomType, DateOnly checkIn, DateOnly checkOut, int guestCount)
    {
        ValidateDates(checkIn, checkOut);

        if (checkOut.DayNumber - checkIn.DayNumber < roomType.MinNights)
            throw InvalidStay($"{roomType.NameEn} needs at least {roomType.MinNights} night(s).");
        if (guestCount > roomType.MaxGuests)
            throw InvalidStay($"{roomType.NameEn} fits at most {roomType.MaxGuests} guests.");
    }

    private static BookingException InvalidStay(string message) =>
        new(StatusCodes.Status400BadRequest, "invalid_stay", message);

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string GenerateReference() =>
        "SBY-" + RandomNumberGenerator.GetString(ReferenceAlphabet, 6);
}
