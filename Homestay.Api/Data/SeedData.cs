using Homestay.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Homestay.Api.Data;

/// <summary>
/// Demo data. Wired into UseSeeding/UseAsyncSeeding in Program.cs, so EF runs it when migrations are
/// applied (dotnet ef database update, or the migrations bundle), never on normal app start.
/// </summary>
public static class SeedData
{
    public static void Seed(DbContext context, IConfiguration configuration)
    {
        if (!context.Set<RoomType>().Any())
            context.Set<RoomType>().AddRange(CreateRoomTypes());

        if (!context.Set<AdminUser>().Any() && CreateAdmin(configuration) is { } admin)
            context.Set<AdminUser>().Add(admin);

        context.SaveChanges();
    }

    public static async Task SeedAsync(
        DbContext context, IConfiguration configuration, CancellationToken cancellationToken)
    {
        if (!await context.Set<RoomType>().AnyAsync(cancellationToken))
            context.Set<RoomType>().AddRange(CreateRoomTypes());

        if (!await context.Set<AdminUser>().AnyAsync(cancellationToken) && CreateAdmin(configuration) is { } admin)
            context.Set<AdminUser>().Add(admin);

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The login comes from configuration, not the repo. With none set, no admin is created.</summary>
    private static AdminUser? CreateAdmin(IConfiguration configuration)
    {
        var email = configuration["Admin:Email"];
        var password = configuration["Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password)) return null;

        var admin = new AdminUser { Email = email.Trim().ToLowerInvariant() };
        admin.PasswordHash = new PasswordHasher<AdminUser>().HashPassword(admin, password);
        return admin;
    }

    private static List<RoomType> CreateRoomTypes() =>
    [
        new()
        {
            Code = "STANDARD",
            NameMs = "Bilik Standard",
            NameEn = "Standard Room",
            DescriptionMs = "Bilik selesa untuk dua orang dengan katil queen, penghawa dingin dan bilik air sendiri.",
            DescriptionEn = "A comfortable room for two with a queen bed, air conditioning and a private bathroom.",
            BasePricePerNight = 150m,
            MaxGuests = 2,
            MinNights = 1,
            ExtraGuestFeePerNight = 0m,
            Rooms = [new() { Number = "S01" }, new() { Number = "S02" }]
        },
        new()
        {
            Code = "FAMILY",
            NameMs = "Bilik Keluarga",
            NameEn = "Family Room",
            DescriptionMs = "Bilik luas untuk keluarga dengan satu katil queen dan dua katil bujang.",
            DescriptionEn = "A spacious family room with one queen bed and two single beds.",
            BasePricePerNight = 250m,
            MaxGuests = 5,
            MinNights = 1,
            ExtraGuestFeePerNight = 30m,
            Rooms = [new() { Number = "F01" }, new() { Number = "F02" }]
        },
        new()
        {
            Code = "CHALET",
            NameMs = "Chalet Tepi Pantai",
            NameEn = "Beachfront Chalet",
            DescriptionMs = "Chalet persendirian menghadap laut dengan anjung, dapur kecil dan ruang untuk enam orang.",
            DescriptionEn = "A private sea-facing chalet with a porch, kitchenette and space for six.",
            BasePricePerNight = 380m,
            MaxGuests = 6,
            MinNights = 2,
            ExtraGuestFeePerNight = 40m,
            Rooms = [new() { Number = "C01" }]
        }
    ];
}
