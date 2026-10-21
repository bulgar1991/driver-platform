using System.Text.Json;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Domain.Common;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider clock)
    : DbContext(options), IAppDbContext
{
    public DbSet<StaffUser> StaffUsers => Set<StaffUser>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<DriverDocument> DriverDocuments => Set<DriverDocument>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<Partner> Partners => Set<Partner>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<VehicleRate> VehicleRates => Set<VehicleRate>();
    public DbSet<SurchargeRule> SurchargeRules => Set<SurchargeRule>();
    public DbSet<ExtraFee> ExtraFees => Set<ExtraFee>();
    public DbSet<PricingSettings> PricingSettings => Set<PricingSettings>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<PartnerWebhook> PartnerWebhooks => Set<PartnerWebhook>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<decimal>().HavePrecision(12, 2);
        builder.Properties<string>().HaveMaxLength(500);

        // Enums are stored as text so the database stays readable.
        builder.Properties<StaffRole>().HaveConversion<string>().HaveMaxLength(32);
        builder.Properties<VehicleCategory>().HaveConversion<string>().HaveMaxLength(32);
        builder.Properties<BookingSource>().HaveConversion<string>().HaveMaxLength(32);
        builder.Properties<BookingStatus>().HaveConversion<string>().HaveMaxLength(32);
        builder.Properties<PaymentMethod>().HaveConversion<string>().HaveMaxLength(32);
        builder.Properties<PaymentStatus>().HaveConversion<string>().HaveMaxLength(32);
        builder.Properties<InvoiceType>().HaveConversion<string>().HaveMaxLength(32);
        builder.Properties<InvoiceStatus>().HaveConversion<string>().HaveMaxLength(32);
        builder.Properties<AssignmentStatus>().HaveConversion<string>().HaveMaxLength(32);
        builder.Properties<DriverDocumentType>().HaveConversion<string>().HaveMaxLength(32);
        builder.Properties<ZoneType>().HaveConversion<string>().HaveMaxLength(32);
        builder.Properties<ZoneShape>().HaveConversion<string>().HaveMaxLength(32);
        builder.Properties<SurchargeKind>().HaveConversion<string>().HaveMaxLength(32);
        builder.Properties<ExtraType>().HaveConversion<string>().HaveMaxLength(32);
        builder.Properties<NotificationChannel>().HaveConversion<string>().HaveMaxLength(32);
        builder.Properties<PriceLineKind>().HaveConversion<string>().HaveMaxLength(32);
        builder.Properties<NotificationKind>().HaveConversion<string>().HaveMaxLength(32);
        builder.Properties<NotificationStatus>().HaveConversion<string>().HaveMaxLength(32);
        builder.Properties<PartnerKind>().HaveConversion<string>().HaveMaxLength(32);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
        foreach (var entry in ChangeTracker.Entries<AuditLog>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }
        }
        await QueuePartnerWebhooksAsync(now, cancellationToken);
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Outbox: a partner booking whose status, shift or shift status changed gets a webhook row in the
    /// same transaction. The payload is thin; the partner reads the details with GET /bookings/{reference}.
    /// </summary>
    private async Task QueuePartnerWebhooksAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var changed = new Dictionary<Guid, (Booking Booking, string Event)>();
        foreach (var entry in ChangeTracker.Entries<Booking>().Where(e => e.Entity.PartnerId != null))
        {
            if (entry.State == EntityState.Added)
            {
                changed[entry.Entity.Id] = (entry.Entity, "booking.created");
            }
            else if (entry.State == EntityState.Modified
                && (entry.Property(b => b.Status).IsModified || entry.Property(b => b.AssignmentId).IsModified))
            {
                changed[entry.Entity.Id] = (entry.Entity, "booking.updated");
            }
        }
        // A driver confirming the shift changes no booking row, but the partner now gets the driver's details.
        var shifts = ChangeTracker.Entries<Assignment>()
            .Where(e => e.State == EntityState.Modified && e.Property(a => a.Status).IsModified)
            .Select(e => e.Entity.Id)
            .ToHashSet();
        foreach (var booking in ChangeTracker.Entries<Booking>()
                     .Select(e => e.Entity)
                     .Where(b => b.PartnerId != null && b.AssignmentId != null && shifts.Contains(b.AssignmentId.Value)))
        {
            changed.TryAdd(booking.Id, (booking, "booking.updated"));
        }
        if (changed.Count == 0)
        {
            return;
        }

        var partnerIds = changed.Values.Select(c => c.Booking.PartnerId!.Value).Distinct().ToList();
        var withWebhook = (await Partners.AsNoTracking()
            .Where(p => partnerIds.Contains(p.Id) && p.WebhookUrl != null && p.IsActive)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken)).ToHashSet();

        foreach (var (booking, @event) in changed.Values.Where(c => withWebhook.Contains(c.Booking.PartnerId!.Value)))
        {
            PartnerWebhooks.Add(new PartnerWebhook
            {
                PartnerId = booking.PartnerId!.Value,
                BookingId = booking.Id,
                Event = @event,
                Payload = JsonSerializer.Serialize(new
                {
                    @event,
                    reference = booking.Reference,
                    externalReference = booking.ExternalReference,
                    status = booking.Status.ToString(),
                    occurredAt = now,
                }),
                NextAttemptAt = now,
                CreatedAt = now,
            });
        }
    }
}
