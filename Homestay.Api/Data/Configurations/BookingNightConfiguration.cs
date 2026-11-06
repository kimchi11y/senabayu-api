using Homestay.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homestay.Api.Data.Configurations;

public class BookingNightConfiguration : IEntityTypeConfiguration<BookingNight>
{
    /// <summary>BookingService recognizes a double booking by this name in the database error.</summary>
    public const string ActiveNightIndex = "UX_BookingNights_Room_Night_Active";

    public void Configure(EntityTypeBuilder<BookingNight> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Status).HasConversion<string>().HasMaxLength(16);

        // The guarantee against double booking. "Partial" because a cancelled night must free the room again.
        builder.HasIndex(n => new { n.RoomId, n.NightDate })
            .IsUnique()
            .HasFilter("\"Status\" <> 'Cancelled'")
            .HasDatabaseName(ActiveNightIndex);

        builder.HasOne(n => n.Booking)
            .WithMany(b => b.BookedNights)
            .HasForeignKey(n => n.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(n => n.Room).WithMany().HasForeignKey(n => n.RoomId).OnDelete(DeleteBehavior.Restrict);
    }
}
