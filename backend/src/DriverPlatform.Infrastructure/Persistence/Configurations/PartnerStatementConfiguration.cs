using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DriverPlatform.Infrastructure.Persistence.Configurations;

internal sealed class PartnerStatementConfiguration : IEntityTypeConfiguration<PartnerStatement>
{
    public void Configure(EntityTypeBuilder<PartnerStatement> builder)
    {
        builder.Property(s => s.Number).HasMaxLength(32);
        builder.HasIndex(s => s.Number).IsUnique();
        builder.HasIndex(s => new { s.Year, s.Sequence }).IsUnique();
        // One statement per partner and month.
        builder.HasIndex(s => new { s.PartnerId, s.Year, s.Month }).IsUnique();
        builder.Property(s => s.Currency).HasMaxLength(3);
        builder.Property(s => s.SentTo).HasMaxLength(254);
        builder.Property(s => s.Note).HasMaxLength(500);
        builder.OwnsMany(s => s.Lines, l =>
        {
            l.ToJson();
            // Local time at the pickup, as on the ride: no offset.
            l.Property(x => x.PickupLocal).HasColumnType("timestamp without time zone");
        });
        builder.HasOne(s => s.Partner).WithMany().OnDelete(DeleteBehavior.Restrict);
    }
}
