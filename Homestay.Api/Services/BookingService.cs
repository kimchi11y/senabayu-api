using System.Security.Cryptography;
using Homestay.Api.Data;
using Homestay.Api.Data.Configurations;
using Homestay.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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

        var freeRooms = FreeRooms(checkIn, checkOut);

        return await db.RoomTypes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.BasePricePerNight)
            .Select(t => new RoomAvailability(t.Code, freeRooms.Count(r => r.RoomTypeId == t.Id)))
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
    /// Takes the first free room of the type. The free-room check only picks a candidate: what actually prevents a
    /// double booking is the unique index on BookingNights, because two requests can both pass the check at once.
    /// The loser of that race tries the next free room, and gets a 409 once none are left.
    /// </summary>
    public async Task<Booking> CreateAsync(NewBooking request, CancellationToken cancellationToken)
    {
        var roomType = await FindRoomTypeAsync(request.RoomTypeCode, cancellationToken);
        ValidateStay(roomType, request.CheckIn, request.CheckOut, request.GuestCount);

        var quote = PricingService.Calculate(roomType, request.CheckIn, request.CheckOut, request.GuestCount);

        // Ends because each failed attempt means another request just took a room, and that room is no longer free.
        while (true)
        {
            var roomId = await FreeRooms(request.CheckIn, request.CheckOut)
                .Where(r => r.RoomTypeId == roomType.Id)
                .OrderBy(r => r.Number)
                .Select(r => (int?)r.Id)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new BookingException(
                    StatusCodes.Status409Conflict, "room_unavailable", "No room of this type is free for those dates.");

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
                CreatedAt = clock.GetUtcNow(),
                BookedNights = Enumerable.Range(0, quote.Nights)
                    .Select(i => new BookingNight { RoomId = roomId, NightDate = request.CheckIn.AddDays(i) })
                    .ToList()
            };

            db.Bookings.Add(booking);
            try
            {
                // One transaction: the booking and all its nights are saved together, or none of them are.
                await db.SaveChangesAsync(cancellationToken);
                return booking;
            }
            catch (DbUpdateException e) when (IsDoubleBooking(e))
            {
                db.Entry(booking).State = EntityState.Detached;
                booking.BookedNights.ForEach(night => db.Entry(night).State = EntityState.Detached);
            }
        }
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

    public async Task<(List<Booking> Items, int Total)> ListAsync(
        BookingStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Bookings.AsNoTracking();
        if (status is not null)
            query = query.Where(b => b.Status == status);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(b => b.RoomType)
            .Include(b => b.Room)
            .OrderByDescending(b => b.CreatedAt)
            .ThenByDescending(b => b.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<Booking> ConfirmAsync(int id, CancellationToken cancellationToken)
    {
        var booking = await LoadForChangeAsync(id, BookingStatus.Confirmed, cancellationToken);
        booking.Status = BookingStatus.Confirmed;
        await db.SaveChangesAsync(cancellationToken);
        return booking;
    }

    /// <summary>Cancelling its nights too is what frees the room: the unique index ignores cancelled nights.</summary>
    public async Task<Booking> CancelAsync(int id, CancellationToken cancellationToken)
    {
        var booking = await LoadForChangeAsync(id, BookingStatus.Cancelled, cancellationToken);
        booking.Status = BookingStatus.Cancelled;
        booking.BookedNights.ForEach(night => night.Status = BookingNightStatus.Cancelled);
        await db.SaveChangesAsync(cancellationToken);
        return booking;
    }

    /// <summary>Nights go with them, through the foreign key's cascade delete.</summary>
    public Task DeleteAllAsync(CancellationToken cancellationToken) =>
        db.Bookings.ExecuteDeleteAsync(cancellationToken);

    private async Task<Booking> LoadForChangeAsync(int id, BookingStatus next, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings
                .Include(b => b.RoomType)
                .Include(b => b.Room)
                .Include(b => b.BookedNights)
                .FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new BookingException(
                StatusCodes.Status404NotFound, "booking_not_found", $"Booking {id} doesn't exist.");

        if (!booking.CanChangeTo(next))
            throw new BookingException(
                StatusCodes.Status409Conflict,
                "invalid_transition",
                $"A {booking.Status.ToString().ToLowerInvariant()} booking can't become {next.ToString().ToLowerInvariant()}.");

        return booking;
    }

    private async Task<RoomType> FindRoomTypeAsync(string code, CancellationToken cancellationToken) =>
        await db.RoomTypes.FirstOrDefaultAsync(t => t.Code == code && t.IsActive, cancellationToken)
        ?? throw new BookingException(
            StatusCodes.Status404NotFound, "room_type_not_found", $"Room type '{code}' doesn't exist.");

    /// <summary>Active rooms with no active night in [checkIn, checkOut). Check-out day itself stays bookable.</summary>
    private IQueryable<Room> FreeRooms(DateOnly checkIn, DateOnly checkOut) =>
        db.Rooms.Where(r => r.IsActive && !db.BookingNights.Any(n =>
            n.RoomId == r.Id
            && n.Status == BookingNightStatus.Active
            && n.NightDate >= checkIn
            && n.NightDate < checkOut));

    private static bool IsDoubleBooking(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: BookingNightConfiguration.ActiveNightIndex
        };

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
