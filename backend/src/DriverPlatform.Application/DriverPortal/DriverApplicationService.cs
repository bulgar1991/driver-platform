using System.ComponentModel.DataAnnotations;
using System.Net;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Bookings;
using DriverPlatform.Application.Common;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DriverPlatform.Application.DriverPortal;

public sealed record DriverApplicationRequest
{
    [Required, MaxLength(100)]
    public required string FirstName { get; init; }
    [Required, MaxLength(100)]
    public required string LastName { get; init; }
    [Required, Phone, MaxLength(32)]
    public required string Phone { get; init; }
    [Required, EmailAddress, MaxLength(254)]
    public required string Email { get; init; }
    [Required, MaxLength(80)]
    public required string City { get; init; }
    public DriverLicenceKind Licence { get; init; }
    public bool HasOwnVehicle { get; init; }
    [MaxLength(120)]
    public string? Vehicle { get; init; }
    [Range(0, 60)]
    public int YearsExperience { get; init; }
    [MaxLength(10)]
    public List<string> Languages { get; init; } = [];
    [MaxLength(2000)]
    public string? Message { get; init; }
    /// <summary>Consent to keep the data to evaluate the application (GDPR).</summary>
    public bool PrivacyConsent { get; init; }
    /// <summary>Hidden field; people leave it empty, bots fill it.</summary>
    [MaxLength(200)]
    public string? Website { get; init; }
}

public sealed record DriverApplicationDto(
    Guid Id, string FirstName, string LastName, string Phone, string Email, string City,
    DriverLicenceKind Licence, bool HasOwnVehicle, string? Vehicle, int YearsExperience,
    IReadOnlyList<string> Languages, string? Message, DriverApplicationStatus Status, DateTimeOffset CreatedAt);

public interface IDriverApplicationService
{
    Task SubmitAsync(DriverApplicationRequest request, string? ip, CancellationToken cancellationToken);
    Task<IReadOnlyList<DriverApplicationDto>> ListAsync(DriverApplicationStatus? status, CancellationToken cancellationToken);
    Task<DriverApplicationDto> SetStatusAsync(Guid id, DriverApplicationStatus status, CancellationToken cancellationToken);
}

/// <summary>"For drivers": applications are saved for the dispatcher and emailed to the company.</summary>
public sealed class DriverApplicationService(
    IAppDbContext db,
    IEmailSender email,
    IAuditLogger audit,
    IOptions<CompanyOptions> company,
    TimeProvider clock,
    ILogger<DriverApplicationService> logger) : IDriverApplicationService
{
    /// <summary>The same phone can apply again after this, not before (stops accidental double sends).</summary>
    private static readonly TimeSpan Cooldown = TimeSpan.FromDays(1);

    public async Task SubmitAsync(DriverApplicationRequest request, string? ip, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(request.Website))
        {
            // Looks accepted to the bot; nothing is stored.
            logger.LogInformation("Driver application dropped by the honeypot from {Ip}", ip);
            return;
        }
        if (!request.PrivacyConsent)
        {
            throw new RequestValidationException("privacyConsent", "Please accept the privacy policy.");
        }

        var phone = BookingFactory.NormalizePhone(request.Phone);
        var since = clock.GetUtcNow() - Cooldown;
        if (await db.DriverApplications.AnyAsync(a => a.Phone == phone && a.CreatedAt > since, cancellationToken))
        {
            return;
        }

        var application = new DriverApplication
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Phone = phone,
            Email = request.Email.Trim().ToLowerInvariant(),
            City = request.City.Trim(),
            Licence = request.Licence,
            HasOwnVehicle = request.HasOwnVehicle,
            Vehicle = request.HasOwnVehicle ? BookingFactory.Normalize(request.Vehicle) : null,
            YearsExperience = request.YearsExperience,
            Languages = request.Languages.Select(l => l.Trim().ToLowerInvariant()).Where(l => l.Length is > 0 and <= 8).Distinct().ToList(),
            Message = BookingFactory.Normalize(request.Message),
            Status = DriverApplicationStatus.New,
            ClientIp = ip,
        };
        db.DriverApplications.Add(application);
        await db.SaveChangesAsync(cancellationToken);
        await NotifyAsync(application, cancellationToken);
    }

    public async Task<IReadOnlyList<DriverApplicationDto>> ListAsync(DriverApplicationStatus? status, CancellationToken cancellationToken)
    {
        var rows = await db.DriverApplications.AsNoTracking()
            .Where(a => status == null || a.Status == status)
            .OrderByDescending(a => a.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public async Task<DriverApplicationDto> SetStatusAsync(Guid id, DriverApplicationStatus status, CancellationToken cancellationToken)
    {
        var application = await db.DriverApplications.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException("Application not found.");
        application.Status = status;
        audit.Log("driver_application.status_changed", nameof(DriverApplication), id.ToString(), new { status });
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(application);
    }

    private async Task NotifyAsync(DriverApplication a, CancellationToken cancellationToken)
    {
        var to = company.Value.Email;
        if (string.IsNullOrWhiteSpace(to))
        {
            return;
        }
        var lines = new[]
        {
            $"{a.FirstName} {a.LastName}, {a.City}",
            $"Phone: {a.Phone} · Email: {a.Email}",
            $"Licence: {a.Licence} · {a.YearsExperience} years of experience",
            a.HasOwnVehicle ? $"Own vehicle: {a.Vehicle ?? "yes"}" : "No own vehicle",
            a.Languages.Count > 0 ? $"Languages: {string.Join(", ", a.Languages)}" : "",
            a.Message ?? "",
        }.Where(l => l.Length > 0).ToList();
        try
        {
            await email.SendAsync(new EmailMessage(
                to, company.Value.Name, $"New driver application: {a.FirstName} {a.LastName} ({a.City})",
                "<div style=\"font-family:Arial,sans-serif;font-size:14px\">" + string.Concat(lines.Select(l => $"<p>{WebUtility.HtmlEncode(l)}</p>")) + "</div>",
                string.Join("\n", lines),
                []), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The application is saved and visible in the panel anyway.
            logger.LogError(ex, "Could not email driver application {Id}", a.Id);
        }
    }

    private static DriverApplicationDto ToDto(DriverApplication a) => new(
        a.Id, a.FirstName, a.LastName, a.Phone, a.Email, a.City, a.Licence, a.HasOwnVehicle, a.Vehicle,
        a.YearsExperience, a.Languages, a.Message, a.Status, a.CreatedAt);
}
