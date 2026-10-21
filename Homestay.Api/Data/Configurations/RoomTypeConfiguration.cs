using Homestay.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homestay.Api.Data.Configurations;

public class RoomTypeConfiguration : IEntityTypeConfiguration<RoomType>
{
    public void Configure(EntityTypeBuilder<RoomType> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Code).IsRequired().HasMaxLength(32);
        builder.HasIndex(t => t.Code).IsUnique();

        builder.Property(t => t.NameMs).IsRequired().HasMaxLength(100);
        builder.Property(t => t.NameEn).IsRequired().HasMaxLength(100);
        builder.Property(t => t.DescriptionMs).IsRequired().HasMaxLength(1000);
        builder.Property(t => t.DescriptionEn).IsRequired().HasMaxLength(1000);

        // numeric(10,2): exact decimal money, up to 99,999,999.99
        builder.Property(t => t.BasePricePerNight).HasPrecision(10, 2);
        builder.Property(t => t.ExtraGuestFeePerNight).HasPrecision(10, 2);

        builder.HasMany(t => t.Rooms)
            .WithOne(r => r.RoomType)
            .HasForeignKey(r => r.RoomTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
