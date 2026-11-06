using Homestay.Api.Services;
using Microsoft.AspNetCore.Http;

namespace Homestay.Tests;

/// <summary>The project's main claim: simultaneous guests can never book the same room for the same night.</summary>
public class BookingRaceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const int Requests = 20;

    [Fact]
    public async Task Simultaneous_requests_for_the_last_room_have_exactly_one_winner()
    {
        var code = await postgres.AddRoomTypeAsync(roomCount: 1);

        var outcomes = await BookSimultaneouslyAsync(code, new DateOnly(2031, 3, 10), new DateOnly(2031, 3, 13));

        Assert.Equal(1, outcomes.Count(o => o == StatusCodes.Status201Created));
        Assert.Equal(Requests - 1, outcomes.Count(o => o == StatusCodes.Status409Conflict));
    }

    [Fact]
    public async Task Simultaneous_requests_fill_every_room_and_no_more()
    {
        var code = await postgres.AddRoomTypeAsync(roomCount: 3);

        var outcomes = await BookSimultaneouslyAsync(code, new DateOnly(2031, 4, 10), new DateOnly(2031, 4, 12));

        Assert.Equal(3, outcomes.Count(o => o == StatusCodes.Status201Created));
        Assert.Equal(Requests - 3, outcomes.Count(o => o == StatusCodes.Status409Conflict));
    }

    [Fact]
    public async Task Overlapping_stays_with_different_dates_still_collide()
    {
        var code = await postgres.AddRoomTypeAsync(roomCount: 1);

        // Every stay includes the night of 12 May, but no two have the same dates.
        var stays = Enumerable.Range(0, Requests)
            .Select(i => (CheckIn: new DateOnly(2031, 5, 12).AddDays(-(i % 3)), CheckOut: new DateOnly(2031, 5, 13).AddDays(i % 4)))
            .ToList();

        var outcomes = await RunSimultaneouslyAsync(stays.Select<(DateOnly CheckIn, DateOnly CheckOut), Func<Task<int>>>(
            stay => () => BookAsync(code, stay.CheckIn, stay.CheckOut)));

        Assert.Equal(1, outcomes.Count(o => o == StatusCodes.Status201Created));
    }

    private Task<int[]> BookSimultaneouslyAsync(string code, DateOnly checkIn, DateOnly checkOut) =>
        RunSimultaneouslyAsync(Enumerable.Range(0, Requests)
            .Select<int, Func<Task<int>>>(_ => () => BookAsync(code, checkIn, checkOut)));

    private static async Task<int[]> RunSimultaneouslyAsync(IEnumerable<Func<Task<int>>> requests)
    {
        var startingGun = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = requests.Select(async request =>
        {
            await startingGun.Task;
            return await request();
        }).ToList();

        startingGun.SetResult();
        return await Task.WhenAll(running);
    }

    /// <summary>Each request gets its own DbContext, as it would in the API. Returns the HTTP status it would produce.</summary>
    private async Task<int> BookAsync(string code, DateOnly checkIn, DateOnly checkOut)
    {
        await using var db = postgres.CreateContext();
        var service = new BookingService(db, TimeProvider.System);
        try
        {
            await service.CreateAsync(
                new NewBooking(code, checkIn, checkOut, 2, "Race Test", "race@example.com", "0123456789"),
                CancellationToken.None);
            return StatusCodes.Status201Created;
        }
        catch (BookingException e)
        {
            return e.StatusCode;
        }
    }
}
