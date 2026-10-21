using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DriverPlatform.Infrastructure.Persistence.Configurations;

internal sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.Property(r => r.TokenHash).HasMaxLength(64);
        builder.HasIndex(r => r.TokenHash).IsUnique();
        builder.HasIndex(r => r.BookingId).IsUnique();
        builder.Property(r => r.Comment).HasMaxLength(2000);
        builder.HasOne(r => r.Booking).WithMany().OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Driver).WithMany().OnDelete(DeleteBehavior.Restrict);
    }
}
