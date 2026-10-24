using DriverPlatform.Application.Auth;
using DriverPlatform.Application.Dispatch;
using DriverPlatform.Application.DriverPortal;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Application.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverPlatform.Api.Controllers.Admin;

/// <summary>The dispatcher panel (business plan §5, §7): bookings, shifts, drivers, vehicles. Admins and dispatchers.</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = Policies.Staff)]
public sealed class DispatchController(
    IBookingAdminService bookings,
    IShiftService shifts,
    IDriverAdminService drivers,
    IVehicleAdminService vehicles,
    IDashboardService dashboard,
    IReviewService reviews,
    IDriverApplicationService applications) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardResponse>> Dashboard(CancellationToken cancellationToken) =>
        Ok(await dashboard.GetAsync(cancellationToken));

    // Bookings

    [HttpGet("bookings")]
    public async Task<ActionResult<PagedResult<BookingListItem>>> Bookings([FromQuery] BookingQuery query, CancellationToken cancellationToken) =>
        Ok(await bookings.ListAsync(query, cancellationToken));

    [HttpGet("bookings/{id:guid}")]
    public async Task<ActionResult<BookingDetail>> Booking(Guid id, CancellationToken cancellationToken) =>
        Ok(await bookings.GetAsync(id, cancellationToken));

    [HttpPut("bookings/{id:guid}/status")]
    public async Task<ActionResult<BookingDetail>> BookingStatus(Guid id, BookingStatusRequest request, CancellationToken cancellationToken) =>
        Ok(await bookings.ChangeStatusAsync(id, request, cancellationToken));

    [HttpPut("bookings/{id:guid}/notes")]
    public async Task<ActionResult<BookingDetail>> BookingNotes(Guid id, BookingNotesRequest request, CancellationToken cancellationToken) =>
        Ok(await bookings.UpdateNotesAsync(id, request, cancellationToken));

    // Shifts

    [HttpGet("shifts")]
    public async Task<ActionResult<IReadOnlyList<ShiftDto>>> Shifts([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        if (to < from || to.DayNumber - from.DayNumber > 62)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["to"] = ["Pick up to two months."] }));
        }
        return Ok(await shifts.ListAsync(from, to, cancellationToken));
    }

    [HttpGet("shifts/availability")]
    public async Task<ActionResult<AvailabilityResponse>> Availability(
        [FromQuery] List<Guid> bookingIds, [FromQuery] Guid? shiftId, CancellationToken cancellationToken) =>
        Ok(await shifts.AvailabilityAsync(bookingIds, shiftId, cancellationToken));

    [HttpGet("shifts/{id:guid}")]
    public async Task<ActionResult<ShiftDto>> Shift(Guid id, CancellationToken cancellationToken) =>
        Ok(await shifts.GetAsync(id, cancellationToken));

    [HttpPost("shifts")]
    public async Task<ActionResult<ShiftResult>> CreateShift(ShiftRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await shifts.CreateAsync(request, cancellationToken));

    [HttpPut("shifts/{id:guid}")]
    public async Task<ActionResult<ShiftResult>> UpdateShift(Guid id, ShiftRequest request, CancellationToken cancellationToken) =>
        Ok(await shifts.UpdateAsync(id, request, cancellationToken));

    [HttpPut("shifts/{id:guid}/status")]
    public async Task<ActionResult<ShiftDto>> ShiftStatus(Guid id, ShiftStatusRequest request, CancellationToken cancellationToken) =>
        Ok(await shifts.ChangeStatusAsync(id, request, cancellationToken));

    // Drivers

    [HttpGet("drivers")]
    public async Task<ActionResult<IReadOnlyList<DriverDto>>> Drivers([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        Ok(await drivers.ListAsync(includeInactive, cancellationToken));

    [HttpGet("drivers/{id:guid}")]
    public async Task<ActionResult<DriverDto>> Driver(Guid id, CancellationToken cancellationToken) =>
        Ok(await drivers.GetAsync(id, cancellationToken));

    [HttpPost("drivers")]
    public async Task<ActionResult<DriverDto>> CreateDriver(DriverRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await drivers.CreateAsync(request, cancellationToken));

    [HttpPut("drivers/{id:guid}")]
    public async Task<ActionResult<DriverDto>> UpdateDriver(Guid id, DriverRequest request, CancellationToken cancellationToken) =>
        Ok(await drivers.UpdateAsync(id, request, cancellationToken));

    /// <summary>The driver's personal link, to copy into a message.</summary>
    [HttpGet("drivers/{id:guid}/link")]
    public async Task<ActionResult<DriverLinkResponse>> DriverLink(Guid id, CancellationToken cancellationToken) =>
        Ok(await drivers.GetLinkAsync(id, cancellationToken));

    /// <summary>Makes a new link; the old one stops working at once.</summary>
    [HttpPost("drivers/{id:guid}/link")]
    public async Task<ActionResult<DriverLinkResponse>> RotateDriverLink(Guid id, CancellationToken cancellationToken) =>
        Ok(await drivers.RotateLinkAsync(id, cancellationToken));

    [HttpDelete("drivers/{id:guid}")]
    public async Task<IActionResult> DeleteDriver(Guid id, CancellationToken cancellationToken)
    {
        await drivers.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPut("drivers/{id:guid}/photo")]
    [RequestSizeLimit(Photos.MaxBytes + 64 * 1024)]
    public async Task<ActionResult<DriverDto>> DriverPhoto(Guid id, IFormFile file, CancellationToken cancellationToken) =>
        Ok(await drivers.SetPhotoAsync(id, await ReadAsync(file, cancellationToken), cancellationToken));

    [HttpDelete("drivers/{id:guid}/photo")]
    public async Task<ActionResult<DriverDto>> RemoveDriverPhoto(Guid id, CancellationToken cancellationToken) =>
        Ok(await drivers.RemovePhotoAsync(id, cancellationToken));

    [HttpGet("reviews")]
    public async Task<ActionResult<IReadOnlyList<ReviewDto>>> Reviews(
        [FromQuery] Guid? driverId, [FromQuery] int take = 200, CancellationToken cancellationToken = default) =>
        Ok(await reviews.ListAsync(driverId, take, cancellationToken));

    [HttpGet("driver-applications")]
    public async Task<ActionResult<IReadOnlyList<DriverApplicationDto>>> DriverApplications(
        [FromQuery] DriverApplicationStatus? status, CancellationToken cancellationToken) =>
        Ok(await applications.ListAsync(status, cancellationToken));

    [HttpPut("driver-applications/{id:guid}/status")]
    public async Task<ActionResult<DriverApplicationDto>> DriverApplicationStatus(
        Guid id, [FromBody] DriverApplicationStatusRequest request, CancellationToken cancellationToken) =>
        Ok(await applications.SetStatusAsync(id, request.Status, cancellationToken));

    // Vehicles

    [HttpGet("vehicles")]
    public async Task<ActionResult<IReadOnlyList<VehicleDto>>> Vehicles([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        Ok(await vehicles.ListAsync(includeInactive, cancellationToken));

    [HttpPost("vehicles")]
    public async Task<ActionResult<VehicleDto>> CreateVehicle(VehicleRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await vehicles.CreateAsync(request, cancellationToken));

    [HttpPut("vehicles/{id:guid}")]
    public async Task<ActionResult<VehicleDto>> UpdateVehicle(Guid id, VehicleRequest request, CancellationToken cancellationToken) =>
        Ok(await vehicles.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("vehicles/{id:guid}")]
    public async Task<IActionResult> DeleteVehicle(Guid id, CancellationToken cancellationToken)
    {
        await vehicles.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPut("vehicles/{id:guid}/photo")]
    [RequestSizeLimit(Photos.MaxBytes + 64 * 1024)]
    public async Task<ActionResult<VehicleDto>> VehiclePhoto(Guid id, IFormFile file, CancellationToken cancellationToken) =>
        Ok(await vehicles.SetPhotoAsync(id, await ReadAsync(file, cancellationToken), cancellationToken));

    [HttpDelete("vehicles/{id:guid}/photo")]
    public async Task<ActionResult<VehicleDto>> RemoveVehiclePhoto(Guid id, CancellationToken cancellationToken) =>
        Ok(await vehicles.RemovePhotoAsync(id, cancellationToken));

    private static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream((int)Math.Min(file.Length, Photos.MaxBytes + 1));
        await file.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }
}

public sealed record DriverApplicationStatusRequest(DriverApplicationStatus Status);
