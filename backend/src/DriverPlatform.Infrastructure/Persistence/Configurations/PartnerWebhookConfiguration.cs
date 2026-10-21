using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DriverPlatform.Infrastructure.Persistence.Configurations;

internal sealed class PartnerWebhookConfiguration : IEntityTypeConfiguration<PartnerWebhook>
{
    public void Configure(EntityTypeBuilder<PartnerWebhook> builder)
    {
        builder.Property(w => w.Event).HasMaxLength(64);
        builder.Property(w => w.Payload).HasMaxLength(4000);
        builder.Property(w => w.LastError).HasMaxLength(500);
        builder.HasIndex(w => new { w.DeliveredAt, w.NextAttemptAt });
        builder.HasIndex(w => w.PartnerId);
        builder.HasOne<Partner>().WithMany().HasForeignKey(w => w.PartnerId).OnDelete(DeleteBehavior.Cascade);
    }
}
