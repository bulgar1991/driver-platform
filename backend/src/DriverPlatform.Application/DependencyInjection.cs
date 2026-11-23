using DriverPlatform.Application.Admin;
using DriverPlatform.Application.Auth;
using DriverPlatform.Application.Bookings;
using DriverPlatform.Application.Dispatch;
using DriverPlatform.Application.DriverPortal;
using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Invoices;
using DriverPlatform.Application.MyBooking;
using DriverPlatform.Application.Notifications;
using DriverPlatform.Application.Partners;
using DriverPlatform.Application.Payments;
using DriverPlatform.Application.Quotes;
using DriverPlatform.Application.Reviews;
using DriverPlatform.Application.Staff;
using Microsoft.Extensions.DependencyInjection;

namespace DriverPlatform.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IQuoteService, QuoteService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IPlaceSearchService, PlaceSearchService>();
        services.AddScoped<IPricingAdminService, PricingAdminService>();
        services.AddScoped<IZoneAdminService, ZoneAdminService>();
        services.AddSingleton<IBookingLinks, BookingLinks>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPaymentAdminService, PaymentAdminService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IBookingEmails, BookingEmails>();
        services.AddScoped<IMyBookingService, MyBookingService>();
        services.AddScoped<IDriverAdminService, DriverAdminService>();
        services.AddScoped<IDriverPayService, DriverPayService>();
        services.AddScoped<IVehicleAdminService, VehicleAdminService>();
        services.AddScoped<IVehicleLogService, VehicleLogService>();
        services.AddScoped<IExpirationService, ExpirationService>();
        services.AddScoped<Customers.ICustomerService, Customers.CustomerService>();
        services.AddScoped<ICalendarService, CalendarService>();
        services.AddScoped<Search.ISearchService, Search.SearchService>();
        services.AddScoped<Statistics.IStatisticsService, Statistics.StatisticsService>();
        services.AddScoped<Messages.IMessageLogService, Messages.MessageLogService>();
        services.AddScoped<Attendance.IAttendanceService, Attendance.AttendanceService>();
        services.AddScoped<IBookingAdminService, BookingAdminService>();
        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddSingleton<IDriverLinks, DriverLinks>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<Content.ISiteContentService, Content.SiteContentService>();
        services.AddScoped<IDriverPortalService, DriverPortalService>();
        services.AddScoped<IDriverApplicationService, DriverApplicationService>();
        services.AddSingleton<IPartnerKeys, PartnerKeys>();
        services.AddSingleton<TwoFactorKeys>();
        services.AddScoped<IPartnerAuthService, PartnerAuthService>();
        services.AddScoped<IPartnerBookingService, PartnerBookingService>();
        services.AddScoped<IPartnerAdminService, PartnerAdminService>();
        services.AddScoped<IPartnerStatementService, PartnerStatementService>();
        services.AddScoped<IStaffAdminService, StaffAdminService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<IHousekeepingService, HousekeepingService>();
        return services;
    }
}
