using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DriverPlatform.Infrastructure.Persistence.Configurations;

internal sealed class PageVisitConfiguration : IEntityTypeConfiguration<PageVisit>
{
    public void Configure(EntityTypeBuilder<PageVisit> builder)
    {
        builder.HasIndex(v => v.At);
        builder.Property(v => v.Path).HasMaxLength(300);
        builder.Property(v => v.Referrer).HasMaxLength(200);
        builder.Property(v => v.Ip).HasMaxLength(45);
        builder.Property(v => v.Country).HasMaxLength(2);
        builder.Property(v => v.City).HasMaxLength(100);
        builder.Property(v => v.Device).HasMaxLength(16);
        builder.Property(v => v.Browser).HasMaxLength(32);
        builder.Property(v => v.Os).HasMaxLength(32);
        builder.Property(v => v.Language).HasMaxLength(8);
        builder.Property(v => v.VisitorHash).HasMaxLength(64);
    }
}
