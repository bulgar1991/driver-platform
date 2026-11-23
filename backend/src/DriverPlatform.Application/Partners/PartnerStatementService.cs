using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Text;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Geo;
using DriverPlatform.Application.Localization;
using DriverPlatform.Application.Payments;
using DriverPlatform.Application.Pricing;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DriverPlatform.Application.Partners;

public enum StatementStatus
{
    /// <summary>Rides in the month, nothing issued yet.</summary>
    NotIssued,
    Issued,
    Sent,
    /// <summary>Issued or sent, not paid, past the due date.</summary>
    Overdue,
    Paid,
}

/// <param name="Changed">Issued, but the rides have changed since (a new ride, a cancellation, a new price): issue it again.</param>
public sealed record StatementRow(
    Guid PartnerId,
    string PartnerName,
    PartnerKind PartnerKind,
    string ContactEmail,
    bool MonthlyInvoicing,
    StatementStatus Status,
    string? Number,
    int Rides,
    int Cancelled,
    decimal Revenue,
    decimal CommissionPercent,
    decimal Commission,
    decimal Net,
    DateTimeOffset? IssuedAt,
    DateOnly? DueOn,
    DateTimeOffset? SentAt,
    string? SentTo,
    DateTimeOffset? PaidAt,
    string? Note,
    bool Changed);

/// <param name="Billed">Net of the statements issued.</param>
/// <param name="Outstanding">Net issued and not paid yet.</param>
/// <param name="ToIssue">Net of the partners with rides and no statement.</param>
public sealed record StatementTotals(int Partners, int NotIssued, decimal ToIssue, decimal Billed, decimal Outstanding, decimal Paid, decimal Commission, int Overdue);

/// <param name="MonthOver">False while the month is running: more rides can still come in.</param>
public sealed record StatementMonth(int Year, int Month, bool MonthOver, IReadOnlyList<StatementRow> Items, StatementTotals Totals, string Currency);

public sealed record StatementLineDto(
    Guid BookingId, string Reference, string? ExternalReference, DateTime PickupLocal, string Passenger, string From, string To,
    VehicleCategory VehicleCategory, BookingStatus Status, decimal Price, decimal Commission);

public sealed record StatementDetail(StatementRow Row, IReadOnlyList<StatementLineDto> Lines, string Currency);

public sealed record StatementSendRequest
{
    /// <summary>Another address than the partner's contact email.</summary>
    [EmailAddress, MaxLength(254)]
    public string? To { get; init; }
}

public sealed record StatementPaidRequest
{
    public bool Paid { get; init; }
    /// <summary>The payment's office date; today when left out.</summary>
    public DateOnly? On { get; init; }
    [MaxLength(500)]
    public string? Note { get; init; }
}

public interface IPartnerStatementService
{
    Task<StatementMonth> MonthAsync(int year, int month, CancellationToken cancellationToken);
    Task<StatementDetail> DetailAsync(Guid partnerId, int year, int month, CancellationToken cancellationToken);
    /// <summary>Freezes the month's rides and amounts with a number and a PDF; again (same number) while not paid.</summary>
    Task<StatementDetail> IssueAsync(Guid partnerId, int year, int month, CancellationToken cancellationToken);
    Task<StatementDetail> SendAsync(Guid partnerId, int year, int month, StatementSendRequest request, CancellationToken cancellationToken);
    Task<StatementDetail> SetPaidAsync(Guid partnerId, int year, int month, StatementPaidRequest request, CancellationToken cancellationToken);
    /// <summary>The issued PDF, or a draft of the live figures.</summary>
    Task<InvoiceFile> PdfAsync(Guid partnerId, int year, int month, CancellationToken cancellationToken);
    Task<InvoiceFile> CsvAsync(Guid partnerId, int year, int month, CancellationToken cancellationToken);
}

/// <summary>
/// Admin → Statements: what each partner owes for a month — the rides at their price, less the agreed
/// commission. A statement is issued (numbered, frozen, PDF), emailed to the partner and marked paid.
/// </summary>
public sealed class PartnerStatementService(
    IAppDbContext db,
    IStatementRenderer renderer,
    IEmailSender email,
    IAuditLogger audit,
    IOptions<CompanyOptions> company,
    TimeProvider clock,
    ILogger<PartnerStatementService> logger) : IPartnerStatementService
{
    /// <summary>Days the partner has to pay, from the day it is issued.</summary>
    public const int PaymentDays = 30;

    public async Task<StatementMonth> MonthAsync(int year, int month, CancellationToken cancellationToken)
    {
        Validate(year, month);
        var (start, end) = OfficeTime.Month(year, month);
        var bookings = await db.Bookings.AsNoTracking()
            .Include(b => b.Client)
            .Where(b => b.PartnerId != null && b.PickupAt >= start && b.PickupAt < end && b.Status != BookingStatus.AwaitingPayment)
            .ToListAsync(cancellationToken);
        var statements = await db.PartnerStatements.AsNoTracking()
            .Where(s => s.Year == year && s.Month == month)
            .ToDictionaryAsync(s => s.PartnerId, cancellationToken);
        var withRides = bookings.Select(b => b.PartnerId!.Value).ToHashSet();
        // Partners billed monthly show even without rides, so a quiet month is visible too.
        var partners = await db.Partners.AsNoTracking()
            .Where(p => (p.IsActive && p.MonthlyInvoicing) || withRides.Contains(p.Id) || statements.Keys.Contains(p.Id))
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        var today = Today();
        var rows = partners.Select(p =>
        {
            var live = Build(p, bookings.Where(b => b.PartnerId == p.Id));
            return ToRow(p, live, statements.GetValueOrDefault(p.Id), today);
        }).ToList();

        var issued = rows.Where(r => r.Status != StatementStatus.NotIssued).ToList();
        var waiting = rows.Where(r => r.Status == StatementStatus.NotIssued && r.Rides + r.Cancelled > 0).ToList();
        var totals = new StatementTotals(
            rows.Count,
            waiting.Count,
            waiting.Sum(r => r.Net),
            issued.Sum(r => r.Net),
            issued.Where(r => r.Status != StatementStatus.Paid).Sum(r => r.Net),
            issued.Where(r => r.Status == StatementStatus.Paid).Sum(r => r.Net),
            rows.Sum(r => r.Commission),
            rows.Count(r => r.Status == StatementStatus.Overdue));
        var monthOver = OfficeTime.ToLocal(clock.GetUtcNow()) >= new DateTime(year, month, 1).AddMonths(1);
        return new StatementMonth(year, month, monthOver, rows, totals, PricingDefaults.Currency);
    }

    public async Task<StatementDetail> DetailAsync(Guid partnerId, int year, int month, CancellationToken cancellationToken)
    {
        var (partner, live, statement) = await LoadAsync(partnerId, year, month, tracking: false, cancellationToken);
        return Detail(partner, live, statement);
    }

    public async Task<StatementDetail> IssueAsync(Guid partnerId, int year, int month, CancellationToken cancellationToken)
    {
        var (partner, live, statement) = await LoadAsync(partnerId, year, month, tracking: true, cancellationToken);
        if (statement?.PaidAt is not null)
        {
            throw new ConflictException(Texts.Current.StatementAlreadyPaid);
        }
        if (live.Lines.Count == 0)
        {
            throw new ConflictException(Texts.Current.StatementEmpty);
        }

        var now = clock.GetUtcNow();
        var again = statement is not null;
        if (statement is null)
        {
            var sequence = (await db.PartnerStatements.Where(s => s.Year == year).MaxAsync(s => (int?)s.Sequence, cancellationToken) ?? 0) + 1;
            statement = new PartnerStatement
            {
                PartnerId = partner.Id,
                Year = year,
                Month = month,
                Sequence = sequence,
                Number = $"{company.Value.InvoicePrefix}-ST-{year}-{sequence:0000}",
            };
            db.PartnerStatements.Add(statement);
        }
        statement.Rides = live.Rides;
        statement.Cancelled = live.Cancelled;
        statement.Revenue = live.Revenue;
        statement.CommissionPercent = live.CommissionPercent;
        statement.Commission = live.Commission;
        statement.Net = live.Net;
        statement.Currency = live.Currency;
        statement.Lines = live.Lines;
        statement.IssuedAt = now;
        statement.DueOn = Today().AddDays(PaymentDays);
        // A new copy has not reached the partner yet.
        statement.SentAt = null;
        statement.SentTo = null;
        statement.Pdf = renderer.Render(statement, partner, draft: false, OfficeTime.ToLocal(now));

        audit.Log(again ? "statement.reissued" : "statement.issued", nameof(PartnerStatement), statement.Id.ToString(),
            new { partner = partner.Name, statement.Number, statement.Rides, statement.Net });
        await db.SaveChangesAsync(cancellationToken);
        return Detail(partner, live, statement);
    }

    public async Task<StatementDetail> SendAsync(
        Guid partnerId, int year, int month, StatementSendRequest request, CancellationToken cancellationToken)
    {
        var (partner, live, statement) = await LoadAsync(partnerId, year, month, tracking: true, cancellationToken);
        if (statement?.Pdf is null)
        {
            throw new ConflictException(Texts.Current.StatementNotIssued);
        }
        var to = (string.IsNullOrWhiteSpace(request.To) ? partner.ContactEmail : request.To).Trim();
        if (to.Length == 0)
        {
            throw new ConflictException(Texts.Current.PartnerHasNoEmail);
        }

        var t = Texts.Current;
        var period = Period(year, month, t);
        var subject = t.StatementSubject(statement.Number, period);
        var body = t.StatementBody(period, statement.Rides, t.Money(statement.Net, statement.Currency), statement.DueOn);
        var c = company.Value;
        var contact = string.Join(" · ", new[] { c.Phone, c.Email }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var paragraphs = new[] { t.Dear(partner.Name), body, $"{t.KindRegards}\n{c.Name}" + (contact.Length > 0 ? $"\n{contact}" : "") };
        var html = "<div style=\"font-family:Arial,Helvetica,sans-serif;color:#1a1a1a;font-size:14px;line-height:1.5\">" +
                   $"<p style=\"font-size:18px;letter-spacing:2px;color:#0B0B0C\">{WebUtility.HtmlEncode(c.Name.ToUpperInvariant())}</p>" +
                   string.Concat(paragraphs.Select(p => $"<p>{WebUtility.HtmlEncode(p).Replace("\n", "<br>")}</p>")) + "</div>";
        var message = new EmailMessage(to, partner.Name, subject, html, string.Join("\n\n", paragraphs),
            [new EmailAttachment($"{statement.Number}.pdf", "application/pdf", statement.Pdf)]);

        // Every attempt goes in the sent-messages log.
        var log = new NotificationLog
        {
            Kind = NotificationKind.PartnerStatement,
            Channel = NotificationChannel.Email,
            Recipient = to,
            Subject = subject.Length > 300 ? subject[..300] : subject,
            CreatedAt = clock.GetUtcNow(),
        };
        db.NotificationLogs.Add(log);
        try
        {
            await email.SendAsync(message, cancellationToken);
            log.Status = NotificationStatus.Sent;
            statement.SentAt = clock.GetUtcNow();
            statement.SentTo = to;
            audit.Log("statement.sent", nameof(PartnerStatement), statement.Id.ToString(), new { statement.Number, to });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not email statement {Number} to {Partner}", statement.Number, partner.Name);
            log.Status = NotificationStatus.Failed;
            log.Error = ex.Message.Length > 480 ? ex.Message[..480] : ex.Message;
            await db.SaveChangesAsync(cancellationToken);
            throw new ConflictException(t.StatementSendFailed(ex.Message));
        }
        return Detail(partner, live, statement);
    }

    public async Task<StatementDetail> SetPaidAsync(
        Guid partnerId, int year, int month, StatementPaidRequest request, CancellationToken cancellationToken)
    {
        var (partner, live, statement) = await LoadAsync(partnerId, year, month, tracking: true, cancellationToken);
        if (statement is null)
        {
            throw new ConflictException(Texts.Current.StatementNotIssued);
        }
        statement.PaidAt = request.Paid
            ? request.On is { } on ? OfficeTime.ToUtc(on.ToDateTime(new TimeOnly(12, 0))) : clock.GetUtcNow()
            : null;
        statement.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        audit.Log(request.Paid ? "statement.paid" : "statement.unpaid", nameof(PartnerStatement), statement.Id.ToString(),
            new { statement.Number, statement.Net, statement.Note });
        await db.SaveChangesAsync(cancellationToken);
        return Detail(partner, live, statement);
    }

    public async Task<InvoiceFile> PdfAsync(Guid partnerId, int year, int month, CancellationToken cancellationToken)
    {
        var (partner, live, statement) = await LoadAsync(partnerId, year, month, tracking: false, cancellationToken);
        if (statement?.Pdf is { } pdf)
        {
            return new InvoiceFile($"{statement.Number}.pdf", pdf);
        }
        var draft = new PartnerStatement
        {
            Year = year,
            Month = month,
            Rides = live.Rides,
            Cancelled = live.Cancelled,
            Revenue = live.Revenue,
            CommissionPercent = live.CommissionPercent,
            Commission = live.Commission,
            Net = live.Net,
            Currency = live.Currency,
            Lines = live.Lines,
        };
        return new InvoiceFile($"statement-draft-{Slug(partner.Name)}-{year}-{month:00}.pdf",
            renderer.Render(draft, partner, draft: true, OfficeTime.ToLocal(clock.GetUtcNow())));
    }

    public async Task<InvoiceFile> CsvAsync(Guid partnerId, int year, int month, CancellationToken cancellationToken)
    {
        var (partner, live, statement) = await LoadAsync(partnerId, year, month, tracking: false, cancellationToken);
        var lines = statement?.Lines ?? live.Lines;
        var it = CultureInfo.GetCultureInfo("it-IT");
        string Money(decimal v) => v.ToString("0.00", it);
        var csv = new StringBuilder("reference;external_reference;pickup;passenger;from;to;vehicle;status;price;commission;net\n");
        foreach (var l in lines.OrderBy(l => l.PickupLocal))
        {
            csv.AppendJoin(';',
                l.Reference,
                Csv(l.ExternalReference ?? ""),
                l.PickupLocal.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                Csv(l.Passenger),
                Csv(l.From),
                Csv(l.To),
                l.VehicleCategory,
                l.Status,
                Money(l.Price),
                Money(l.Commission),
                Money(l.Price - l.Commission)).Append('\n');
        }
        var total = lines.Sum(l => l.Price);
        var commission = lines.Sum(l => l.Commission);
        csv.Append(CultureInfo.InvariantCulture, $"TOTAL;;;;;;;;{Money(total)};{Money(commission)};{Money(total - commission)}\n");

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return new InvoiceFile($"statement-{Slug(partner.Name)}-{year}-{month:00}.csv", bytes);
    }

    // ── Building ──────────────────────────────────────────────────────────────

    private sealed record Figures(
        List<PartnerStatementLine> Lines, int Rides, int Cancelled, decimal Revenue, decimal CommissionPercent, decimal Commission,
        decimal Net, string Currency);

    private static Figures Build(Partner partner, IEnumerable<Booking> bookings)
    {
        var lines = bookings.OrderBy(b => b.PickupAt).Select(b =>
        {
            // Cancelled rides are listed for reference but not charged.
            var price = b.Status == BookingStatus.Cancelled ? 0 : b.TotalPrice;
            return new PartnerStatementLine
            {
                BookingId = b.Id,
                Reference = b.Reference,
                ExternalReference = b.ExternalReference,
                PickupLocal = RideTime.ToLocal(b.PickupAt, b.Pickup),
                Passenger = $"{b.Client.FirstName} {b.Client.LastName}".Trim(),
                From = b.PickupMeetingPoint?.Name ?? b.Pickup.Address,
                To = b.DropoffMeetingPoint?.Name ?? b.Dropoff.Address,
                VehicleCategory = b.VehicleCategory,
                Status = b.Status,
                Price = price,
                Commission = Math.Round(price * partner.CommissionPercent / 100, 2, MidpointRounding.AwayFromZero),
            };
        }).ToList();
        var cancelled = lines.Count(l => l.Status == BookingStatus.Cancelled);
        var revenue = lines.Sum(l => l.Price);
        var commission = lines.Sum(l => l.Commission);
        return new Figures(lines, lines.Count - cancelled, cancelled, revenue, partner.CommissionPercent, commission,
            revenue - commission, PricingDefaults.Currency);
    }

    private async Task<(Partner Partner, Figures Live, PartnerStatement? Statement)> LoadAsync(
        Guid partnerId, int year, int month, bool tracking, CancellationToken cancellationToken)
    {
        Validate(year, month);
        var partner = await db.Partners.AsNoTracking().FirstOrDefaultAsync(p => p.Id == partnerId, cancellationToken)
            ?? throw new NotFoundException(Texts.Current.PartnerNotFound);
        var (start, end) = OfficeTime.Month(year, month);
        var bookings = await db.Bookings.AsNoTracking()
            .Include(b => b.Client)
            .Where(b => b.PartnerId == partnerId && b.PickupAt >= start && b.PickupAt < end && b.Status != BookingStatus.AwaitingPayment)
            .ToListAsync(cancellationToken);
        var statements = tracking ? db.PartnerStatements : db.PartnerStatements.AsNoTracking();
        var statement = await statements.FirstOrDefaultAsync(s => s.PartnerId == partnerId && s.Year == year && s.Month == month, cancellationToken);
        return (partner, Build(partner, bookings), statement);
    }

    private StatementDetail Detail(Partner partner, Figures live, PartnerStatement? statement)
    {
        var lines = (statement?.Lines ?? live.Lines).OrderBy(l => l.PickupLocal).Select(l => new StatementLineDto(
            l.BookingId, l.Reference, l.ExternalReference, l.PickupLocal, l.Passenger, l.From, l.To, l.VehicleCategory, l.Status,
            l.Price, l.Commission)).ToList();
        return new StatementDetail(ToRow(partner, live, statement, Today()), lines, statement?.Currency ?? live.Currency);
    }

    private static StatementRow ToRow(Partner p, Figures live, PartnerStatement? s, DateOnly today)
    {
        if (s is null)
        {
            return new StatementRow(p.Id, p.Name, p.Kind, p.ContactEmail, p.MonthlyInvoicing, StatementStatus.NotIssued, null,
                live.Rides, live.Cancelled, live.Revenue, live.CommissionPercent, live.Commission, live.Net,
                null, null, null, null, null, null, false);
        }
        var status = s.PaidAt is not null ? StatementStatus.Paid
            : today > s.DueOn ? StatementStatus.Overdue
            : s.SentAt is not null ? StatementStatus.Sent
            : StatementStatus.Issued;
        var changed = s.PaidAt is null && Fingerprint(s.Lines) != Fingerprint(live.Lines);
        return new StatementRow(p.Id, p.Name, p.Kind, p.ContactEmail, p.MonthlyInvoicing, status, s.Number,
            s.Rides, s.Cancelled, s.Revenue, s.CommissionPercent, s.Commission, s.Net,
            s.IssuedAt, s.DueOn, s.SentAt, s.SentTo, s.PaidAt, s.Note, changed);
    }

    /// <summary>What a statement depends on: the rides, their status and price.</summary>
    private static string Fingerprint(IEnumerable<PartnerStatementLine> lines) =>
        string.Join('|', lines.OrderBy(l => l.BookingId).Select(l => $"{l.BookingId}:{l.Status}:{l.Price.ToString(CultureInfo.InvariantCulture)}"));

    private DateOnly Today() => DateOnly.FromDateTime(OfficeTime.ToLocal(clock.GetUtcNow()));

    private static void Validate(int year, int month)
    {
        if (month is < 1 or > 12 || year is < 2020 or > 2100)
        {
            throw new RequestValidationException(new Dictionary<string, string[]> { ["month"] = ["Pick a month."] });
        }
    }

    public static string Period(int year, int month, Texts t)
    {
        var name = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.GetCultureInfo(t.Language));
        return char.ToUpper(name[0], CultureInfo.InvariantCulture) + name[1..];
    }

    private static string Slug(string name) =>
        new string(name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');

    private static string Csv(string value)
    {
        // A leading = + - @ would make a spreadsheet run the cell as a formula.
        if (value.Length > 0 && "=+-@".Contains(value[0]))
        {
            value = "'" + value;
        }
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
