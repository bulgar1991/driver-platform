namespace DriverPlatform.Application.Bookings;

public interface IBookingService
{
    Task<CreateBookingResponse> CreateAsync(CreateBookingRequest request, string? clientIp, CancellationToken cancellationToken);
}
