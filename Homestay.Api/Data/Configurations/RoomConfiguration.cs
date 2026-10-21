using Homestay.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homestay.Api.Data.Configurations;

public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Number).IsRequired().HasMaxLength(16);
        builder.HasIndex(r => r.Number).IsUnique();

        // The RoomType relationship is configured in RoomTypeConfiguration.
    }
}
