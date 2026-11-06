using Homestay.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homestay.Api.Data.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Reference).IsRequired().HasMaxLength(16);
        builder.HasIndex(b => b.Reference).IsUnique();

        builder.Property(b => b.GuestName).IsRequired().HasMaxLength(100);
        builder.Property(b => b.Email).IsRequired().HasMaxLength(254);
        builder.Property(b => b.Phone).IsRequired().HasMaxLength(32);

        builder.Property(b => b.PricePerNightSnapshot).HasPrecision(10, 2);
        builder.Property(b => b.ExtraGuestFee).HasPrecision(10, 2);
        builder.Property(b => b.TotalAmount).HasPrecision(10, 2);
        builder.Property(b => b.Currency).IsRequired().HasMaxLength(3);

        // Stored as text, so the column reads "Pending" rather than 0.
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(16);

        builder.HasOne(b => b.RoomType).WithMany().HasForeignKey(b => b.RoomTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.Room).WithMany().HasForeignKey(b => b.RoomId).OnDelete(DeleteBehavior.Restrict);
    }
}
