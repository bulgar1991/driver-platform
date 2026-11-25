using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DriverPlatform.Infrastructure.Persistence.Configurations;

internal sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.Property(c => c.Email).HasMaxLength(254);
        builder.Property(c => c.Phone).HasMaxLength(32);
        builder.Property(c => c.Language).HasMaxLength(8);
        builder.Property(c => c.NotesForDriver).HasMaxLength(1000);
        builder.Property(c => c.Company).HasMaxLength(150);
        builder.Property(c => c.TaxCode).HasMaxLength(32);
        builder.Property(c => c.VatNumber).HasMaxLength(32);
        builder.Property(c => c.RecipientCode).HasMaxLength(7);
        builder.Property(c => c.PecEmail).HasMaxLength(254);
        builder.Property(c => c.BillingEmail).HasMaxLength(254);
        builder.Property(c => c.Address).HasMaxLength(200);
        builder.Property(c => c.PostalCode).HasMaxLength(16);
        builder.Property(c => c.City).HasMaxLength(100);
        builder.Property(c => c.Province).HasMaxLength(50);
        builder.Property(c => c.Country).HasMaxLength(2);
        builder.Property(c => c.OfficeNotes).HasMaxLength(1000);
        builder.HasIndex(c => c.Email);
        builder.HasIndex(c => c.Phone);
    }
}
