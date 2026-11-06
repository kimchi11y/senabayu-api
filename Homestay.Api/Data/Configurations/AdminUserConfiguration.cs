using Homestay.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homestay.Api.Data.Configurations;

public class AdminUserConfiguration : IEntityTypeConfiguration<AdminUser>
{
    public void Configure(EntityTypeBuilder<AdminUser> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Email).IsRequired().HasMaxLength(254);
        builder.HasIndex(a => a.Email).IsUnique();

        builder.Property(a => a.PasswordHash).IsRequired().HasMaxLength(512);
        builder.Property(a => a.Role).IsRequired().HasMaxLength(32);
    }
}
