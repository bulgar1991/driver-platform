using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DriverPlatform.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.Property(p => p.Currency).HasMaxLength(3);
        builder.Property(p => p.Provider).HasMaxLength(32);
        builder.Property(p => p.CheckoutSessionId).HasMaxLength(255);
        builder.Property(p => p.ProviderTransactionId).HasMaxLength(255);
        builder.HasIndex(p => p.CheckoutSessionId).IsUnique();
        builder.HasIndex(p => p.ProviderTransactionId).IsUnique();
        builder.HasIndex(p => new { p.Status, p.CreatedAt });
        builder.HasOne(p => p.Booking).WithMany(b => b.Payments).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.Property(r => r.Reason).HasMaxLength(300);
        builder.Property(r => r.ProviderRefundId).HasMaxLength(255);
        builder.Property(r => r.ProviderStatus).HasMaxLength(32);
        builder.HasOne(r => r.Payment).WithMany(p => p.Refunds).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StaffUser>().WithMany().HasForeignKey(r => r.StaffUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
