using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DriverPlatform.Infrastructure.Persistence.Configurations;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.Property(i => i.Number).HasMaxLength(32);
        builder.HasIndex(i => i.Number).IsUnique();
        // No gaps, no duplicates: one sequence per document type and year.
        builder.HasIndex(i => new { i.Type, i.Year, i.Sequence }).IsUnique();
        builder.HasIndex(i => i.IssuedAt);
        builder.Property(i => i.Currency).HasMaxLength(3);
        builder.OwnsOne(i => i.Seller, s => s.ToJson());
        builder.OwnsOne(i => i.Buyer, s => s.ToJson());
        builder.OwnsMany(i => i.Lines, l => l.ToJson());
        builder.HasOne(i => i.Booking).WithMany(b => b.Invoices).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.Payment).WithMany().OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.Refund).WithMany().OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.OriginalInvoice).WithMany().OnDelete(DeleteBehavior.Restrict);
    }
}
