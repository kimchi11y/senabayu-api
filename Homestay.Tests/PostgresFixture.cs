using Homestay.Api.Data;
using Homestay.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Homestay.Tests;

/// <summary>A real Postgres in Docker, shared by a test class: races only show up on a real database.</summary>
public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private int _nextRoomNumber;

    public virtual async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public virtual Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public string ConnectionString => _container.GetConnectionString();

    public AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);

    /// <summary>Adds a room type of its own, so tests sharing the database can't affect each other.</summary>
    public async Task<string> AddRoomTypeAsync(int roomCount)
    {
        var code = $"TEST{Guid.NewGuid():N}"[..20];
        await using var db = CreateContext();
        db.RoomTypes.Add(new RoomType
        {
            Code = code,
            NameMs = code,
            NameEn = code,
            DescriptionMs = "Test",
            DescriptionEn = "Test",
            BasePricePerNight = 100m,
            MaxGuests = 4,
            MinNights = 1,
            ExtraGuestFeePerNight = 10m,
            Rooms = Enumerable.Range(0, roomCount)
                .Select(_ => new Room { Number = $"T{Interlocked.Increment(ref _nextRoomNumber)}" })
                .ToList()
        });
        await db.SaveChangesAsync();
        return code;
    }
}
