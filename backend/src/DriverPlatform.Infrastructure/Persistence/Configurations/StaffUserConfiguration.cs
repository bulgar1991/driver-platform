using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DriverPlatform.Infrastructure.Persistence.Configurations;

internal sealed class StaffUserConfiguration : IEntityTypeConfiguration<StaffUser>
{
    public void Configure(EntityTypeBuilder<StaffUser> builder)
    {
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.Email).HasMaxLength(254);
        builder.Property(u => u.TwoFactorSecret).HasMaxLength(200);
        builder.Property(u => u.TwoFactorPendingSecret).HasMaxLength(200);
        builder.Property(u => u.TwoFactorEmailCodeHash).HasMaxLength(64);
    }
}
