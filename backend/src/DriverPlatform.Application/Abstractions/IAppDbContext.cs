using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DriverPlatform.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<StaffUser> StaffUsers { get; }
    DbSet<Client> Clients { get; }
    DbSet<Booking> Bookings { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Refund> Refunds { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<Driver> Drivers { get; }
    DbSet<DriverDocument> DriverDocuments { get; }
    DbSet<Vehicle> Vehicles { get; }
    DbSet<Assignment> Assignments { get; }
    DbSet<Partner> Partners { get; }
    DbSet<Zone> Zones { get; }
    DbSet<VehicleRate> VehicleRates { get; }
    DbSet<SurchargeRule> SurchargeRules { get; }
    DbSet<ExtraFee> ExtraFees { get; }
    DbSet<PricingSettings> PricingSettings { get; }
    DbSet<Review> Reviews { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<NotificationLog> NotificationLogs { get; }
    DbSet<PartnerWebhook> PartnerWebhooks { get; }
    DbSet<DriverApplication> DriverApplications { get; }
    DbSet<Photo> Photos { get; }
    DbSet<SiteContent> SiteContents { get; }
    DbSet<PageVisit> PageVisits { get; }
    DbSet<AttendanceType> AttendanceTypes { get; }
    DbSet<AttendanceEntry> AttendanceEntries { get; }
    DbSet<VehicleExpiration> VehicleExpirations { get; }
    DbSet<VehicleRefuel> VehicleRefuels { get; }
    DbSet<BookingComRide> BookingComRides { get; }
    DbSet<PartnerStatement> PartnerStatements { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
