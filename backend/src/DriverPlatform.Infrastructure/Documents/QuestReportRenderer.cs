using System.Globalization;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Localization;
using DriverPlatform.Application.Search;
using DriverPlatform.Domain.Enums;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DriverPlatform.Infrastructure.Documents;

/// <summary>
/// The PDF reports of Admin → Search, A4 landscape, in the language of the panel. Same look as the invoices:
/// the company name, a gold rule, plain tables and the totals at the end.
/// </summary>
public sealed class QuestReportRenderer(IOptions<CompanyOptions> company) : IReportRenderer
{
    private const string Ink = "#0B0B0C";
    private const string Gold = "#A8873F";
    private const string Muted = "#6B6B6B";
    private const string Line = "#DDDDDD";

    static QuestReportRenderer()
    {
        // Free for companies under USD 1M annual revenue; see questpdf.com/license.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private sealed record Column(string Header, float Width, bool Right, Func<SearchRide, string> Value);

    public byte[] Render(ReportRequest report)
    {
        var t = Texts.Current;
        string W(ReportWord word) => t.Report(word);
        var culture = CultureInfo.GetCultureInfo(t.Language);
        var title = report.Kind switch
        {
            ReportKind.DailySheet => W(ReportWord.DailyTitle),
            ReportKind.Driver => W(ReportWord.DriverTitle),
            ReportKind.Partner => W(ReportWord.PartnerTitle),
            ReportKind.Vehicle => W(ReportWord.VehicleTitle),
            _ => W(ReportWord.ListTitle),
        };
        var period = report.Query.From == report.Query.To
            ? report.Query.From.ToString("dddd d MMMM yyyy", culture)
            : $"{report.Query.From.ToString("d MMM yyyy", culture)} – {report.Query.To.ToString("d MMM yyyy", culture)}";

        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(32);
            page.DefaultTextStyle(s => s.FontSize(8.5f).FontColor(Ink));

            page.Header().Column(header =>
            {
                header.Item().Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Text(company.Value.Name.ToUpperInvariant()).FontSize(15).LetterSpacing(0.15f).Bold();
                        if (!string.IsNullOrWhiteSpace(company.Value.Address))
                        {
                            left.Item().Text(company.Value.Address).FontColor(Muted);
                        }
                    });
                    row.ConstantItem(320).AlignRight().Column(right =>
                    {
                        right.Item().AlignRight().Text(title.ToUpperInvariant()).FontSize(11).Bold().FontColor(Gold).LetterSpacing(0.08f);
                        if (report.Subject is not null)
                        {
                            right.Item().AlignRight().Text(report.Subject).FontSize(11).Bold();
                        }
                        right.Item().AlignRight().Text($"{W(ReportWord.Period)}: {period}");
                        right.Item().AlignRight().Text($"{W(ReportWord.Generated)}: {report.GeneratedAt:dd.MM.yyyy HH:mm}").FontColor(Muted);
                    });
                });
                var filters = Filters(report.Query, t);
                if (filters.Length > 0)
                {
                    header.Item().PaddingTop(4).Text($"{W(ReportWord.Filters)}: {filters}").FontColor(Muted);
                }
                header.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Gold);
            });

            page.Content().Column(content =>
            {
                content.Spacing(10);
                if (report.Truncated)
                {
                    content.Item().Background("#FFF4E0").Padding(6).Text(W(ReportWord.Truncated)).FontColor("#8A5A00");
                }

                if (report.Kind == ReportKind.DailySheet)
                {
                    DailySheet(content, report, culture, W);
                }
                else
                {
                    content.Item().Element(c => Table(c, Columns(report, culture, W), report.Rides));
                }

                content.Item().AlignRight().Width(260).Column(totals =>
                {
                    Total(totals, W(ReportWord.Rides), report.Totals.Rides.ToString(culture));
                    if (report.Totals.Cancelled > 0)
                    {
                        Total(totals, W(ReportWord.Cancelled), report.Totals.Cancelled.ToString(culture));
                    }
                    Total(totals, W(ReportWord.Passengers), report.Totals.Passengers.ToString(culture));
                    Total(totals, W(ReportWord.Distance), $"{report.Totals.Km.ToString("N0", culture)} km");
                    if (report.Kind != ReportKind.Vehicle && report.Kind != ReportKind.DailySheet)
                    {
                        if (report.Kind == ReportKind.Driver && report.IncludePay)
                        {
                            Grand(totals, W(ReportWord.Pay), Money(report.Totals.DriverPay ?? 0, report.Currency, culture));
                        }
                        else
                        {
                            Grand(totals, W(ReportWord.Total), Money(report.Totals.Revenue, report.Currency, culture));
                        }
                    }
                });
            });

            page.Footer().Row(row =>
            {
                row.RelativeItem().Text(company.Value.Name).FontColor(Muted).FontSize(7.5f);
                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.DefaultTextStyle(s => s.FontColor(Muted).FontSize(7.5f));
                    text.Span($"{W(ReportWord.Page)} ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        })).GeneratePdf();
    }

    private static List<Column> Columns(ReportRequest report, CultureInfo culture, Func<ReportWord, string> w)
    {
        var t = Texts.Current;
        var date = new Column(w(ReportWord.Date), 58, false, r => r.PickupLocal.ToString("ddd dd.MM.yy", culture));
        var time = new Column(w(ReportWord.Time), 32, false, r => r.PickupLocal.ToString("HH:mm", culture));
        var reference = new Column(w(ReportWord.Reference), 62, false, r => r.ExternalReference is null ? r.Reference : $"{r.Reference}\n{r.ExternalReference}");
        var customer = new Column(w(ReportWord.Customer), 0, false, r => r.PartnerName is null || report.Kind == ReportKind.Partner ? r.ClientName : $"{r.ClientName}\n{r.PartnerName}");
        var route = new Column(w(ReportWord.Route), 0, false, r => $"{r.PickupLabel}\n→ {r.DropoffLabel}");
        var pax = new Column(w(ReportWord.Pax), 26, true, r => r.Passengers.ToString(culture));
        var km = new Column(w(ReportWord.Km), 34, true, r => r.DistanceKm.ToString("N0", culture));
        var category = new Column(w(ReportWord.Class), 52, false, r => t.Category(r.VehicleCategory));
        var resource = new Column($"{w(ReportWord.Driver)} · {w(ReportWord.Vehicle)}", 92, false,
            r => r.DriverName is null ? w(ReportWord.NotAssigned) : $"{r.DriverName}\n{r.VehiclePlate}");
        var driver = new Column(w(ReportWord.Driver), 92, false, r => r.DriverName ?? w(ReportWord.NotAssigned));
        var vehicle = new Column(w(ReportWord.Vehicle), 62, false, r => r.VehiclePlate ?? "—");
        var status = new Column(w(ReportWord.Status), 58, false, r => Capital(t.BookingStatusName(r.Status)));
        var price = new Column(w(ReportWord.Price), 58, true, r => Money(r.TotalPrice, report.Currency, culture));
        var pay = new Column(w(ReportWord.Pay), 52, true, r => r.DriverPay is { } p ? Money(p, report.Currency, culture) : "—");

        return report.Kind switch
        {
            ReportKind.Driver => [date, time, reference, customer, route, km, vehicle, status, .. report.IncludePay ? new[] { pay } : []],
            ReportKind.Partner => [date, time, reference, customer, route, pax, category, status, price],
            ReportKind.Vehicle => [date, time, reference, route, km, driver, status],
            _ => [date, time, reference, customer, route, pax, category, resource, status, price, .. report.IncludePay ? new[] { pay } : []],
        };
    }

    private static void Table(IContainer container, List<Column> columns, IEnumerable<SearchRide> rides)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                foreach (var column in columns)
                {
                    if (column.Width > 0)
                    {
                        c.ConstantColumn(column.Width);
                    }
                    else
                    {
                        c.RelativeColumn();
                    }
                }
            });
            table.Header(h =>
            {
                foreach (var column in columns)
                {
                    var cell = h.Cell().BorderBottom(1).BorderColor(Ink).PaddingBottom(3).PaddingRight(4);
                    (column.Right ? cell.AlignRight() : cell).Text(column.Header).FontSize(7.5f).Bold();
                }
            });
            foreach (var ride in rides)
            {
                var cancelled = ride.Status == BookingStatus.Cancelled;
                foreach (var column in columns)
                {
                    var cell = table.Cell().BorderBottom(0.5f).BorderColor(Line).PaddingVertical(3).PaddingRight(4);
                    var text = (column.Right ? cell.AlignRight() : cell).Text(column.Value(ride));
                    if (cancelled)
                    {
                        text.FontColor(Muted).Strikethrough();
                    }
                }
            }
        });
    }

    /// <summary>One block per driver (rides not assigned first), each ride with what the driver needs on the road.</summary>
    private static void DailySheet(ColumnDescriptor content, ReportRequest report, CultureInfo culture, Func<ReportWord, string> w)
    {
        var time = new Column(w(ReportWord.Time), 36, false, r => r.PickupLocal.ToString("HH:mm", culture));
        var reference = new Column(w(ReportWord.Reference), 62, false, r => r.Reference);
        var customer = new Column(w(ReportWord.Customer), 150, false,
            r => r.PartnerName is null ? $"{r.ClientName}\n{r.ClientPhone}" : $"{r.ClientName}\n{r.ClientPhone} · {r.PartnerName}");
        // Passengers / bags.
        var pax = new Column(w(ReportWord.Pax), 40, false, r => $"{r.Passengers} / {r.LargeLuggage + r.SmallLuggage}");
        var route = new Column(w(ReportWord.Route), 0, false, r => $"{r.PickupLabel}\n→ {r.DropoffLabel}");
        var flight = new Column(w(ReportWord.Flight), 50, false, r => r.FlightNumber ?? "");
        var status = new Column(w(ReportWord.Status), 62, false, r => Capital(Texts.Current.BookingStatusName(r.Status)));
        List<Column> columns = [time, reference, customer, pax, route, flight, status];

        foreach (var group in report.Rides.GroupBy(r => r.DriverId).OrderBy(g => g.Key is null ? 0 : 1).ThenBy(g => g.First().DriverName))
        {
            var first = group.First();
            content.Item().ShowEntire().Column(block =>
            {
                block.Item().PaddingTop(4).Text(first.DriverName is null
                    ? w(ReportWord.NotAssigned)
                    : $"{first.DriverName} · {string.Join(", ", group.Select(r => r.VehiclePlate).Distinct())}")
                    .FontSize(10).Bold().FontColor(first.DriverName is null ? "#B45309" : Ink);
                block.Item().PaddingTop(3).Element(c => Table(c, columns, group.OrderBy(r => r.PickupLocal)));
            });
        }
    }

    private static string Filters(SearchQuery q, Texts t)
    {
        var parts = new List<string>();
        if (q.Statuses.Count > 0)
        {
            parts.Add(string.Join(", ", q.Statuses.Select(t.BookingStatusName)));
        }
        if (q.Source is { } source)
        {
            parts.Add(source.ToString());
        }
        if (q.Category is { } category)
        {
            parts.Add(t.Category(category));
        }
        if (q.Payment != PaymentFilter.Any)
        {
            parts.Add(q.Payment.ToString());
        }
        if (q.Unassigned)
        {
            parts.Add(t.Report(ReportWord.NotAssigned));
        }
        if (!string.IsNullOrWhiteSpace(q.Text))
        {
            parts.Add($"“{q.Text.Trim()}”");
        }
        return string.Join(" · ", parts);
    }

    private static void Total(ColumnDescriptor totals, string label, string value) =>
        totals.Item().PaddingVertical(1).Row(row =>
        {
            row.RelativeItem().Text(label).FontColor(Muted);
            row.AutoItem().Text(value);
        });

    private static void Grand(ColumnDescriptor totals, string label, string value) =>
        totals.Item().PaddingTop(4).BorderTop(1).BorderColor(Gold).PaddingTop(4).Row(row =>
        {
            row.RelativeItem().Text(label).Bold();
            row.AutoItem().Text(value).Bold().FontSize(11);
        });

    private static string Capital(string text) =>
        text.Length == 0 ? text : char.ToUpper(text[0], CultureInfo.InvariantCulture) + text[1..];

    private static string Money(decimal amount, string currency, CultureInfo culture) =>
        $"{currency} {amount.ToString("N2", culture)}";
}
