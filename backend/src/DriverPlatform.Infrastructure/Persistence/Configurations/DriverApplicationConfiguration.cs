using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DriverPlatform.Infrastructure.Persistence.Configurations;

internal sealed class DriverApplicationConfiguration : IEntityTypeConfiguration<DriverApplication>
{
    public void Configure(EntityTypeBuilder<DriverApplication> builder)
    {
        builder.Property(a => a.Phone).HasMaxLength(32);
        builder.Property(a => a.Email).HasMaxLength(254);
        builder.Property(a => a.Message).HasMaxLength(2000);
        builder.Property(a => a.ClientIp).HasMaxLength(45);
        builder.HasIndex(a => new { a.Status, a.CreatedAt });
        builder.HasIndex(a => a.Phone);
    }
}
