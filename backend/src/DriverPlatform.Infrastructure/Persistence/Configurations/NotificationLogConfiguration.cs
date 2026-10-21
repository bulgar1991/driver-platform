using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DriverPlatform.Infrastructure.Persistence.Configurations;

internal sealed class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> builder)
    {
        builder.Property(n => n.Recipient).HasMaxLength(254);
        builder.Property(n => n.ProviderMessageId).HasMaxLength(128);
        builder.HasIndex(n => n.AssignmentId);
        builder.HasIndex(n => n.BookingId);
        builder.HasIndex(n => n.DriverId);
    }
}
