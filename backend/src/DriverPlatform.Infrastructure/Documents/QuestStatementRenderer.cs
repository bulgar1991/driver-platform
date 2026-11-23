using System.Globalization;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Localization;
using DriverPlatform.Application.Partners;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DriverPlatform.Infrastructure.Documents;

/// <summary>
/// A partner's monthly statement, A4 portrait, in the language of the panel: who it is for, the rides with
/// their price and commission, and the net amount due. Same look as the invoices and the reports.
/// </summary>
public sealed class QuestStatementRenderer(IOptions<CompanyOptions> company) : IStatementRenderer
{
    private const string Ink = "#0B0B0C";
    private const string Gold = "#A8873F";
    private const string Muted = "#6B6B6B";
    private const string Line = "#DDDDDD";

    static QuestStatementRenderer()
    {
        // Free for companies under USD 1M annual revenue; see questpdf.com/license.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(PartnerStatement statement, Partner partner, bool draft, DateTime generatedAt)
    {
        var t = Texts.Current;
        string W(ReportWord word) => t.Report(word);
        var culture = CultureInfo.GetCultureInfo(t.Language);
        var c = company.Value;
        string Money(decimal v) => $"{statement.Currency} {v.ToString("N2", culture)}";

        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(s => s.FontSize(8.5f).FontColor(Ink));

            page.Header().Column(header =>
            {
                header.Item().Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Text(c.Name.ToUpperInvariant()).FontSize(15).LetterSpacing(0.15f).Bold();
                        foreach (var line in new[] { c.Address, c.VatNumber.Length > 0 ? $"VAT {c.VatNumber}" : "", c.Email, c.Phone })
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                left.Item().Text(line).FontColor(Muted);
                            }
                        }
                    });
                    row.ConstantItem(220).AlignRight().Column(right =>
                    {
                        right.Item().AlignRight().Text(W(ReportWord.StatementTitle).ToUpperInvariant()).FontSize(13).Bold().FontColor(Gold).LetterSpacing(0.08f);
                        right.Item().AlignRight().Text(PartnerStatementService.Period(statement.Year, statement.Month, t)).FontSize(11).Bold();
                        if (draft)
                        {
                            right.Item().AlignRight().Text(W(ReportWord.Draft)).FontColor("#B45309").Bold();
                        }
                        else
                        {
                            right.Item().AlignRight().Text($"{W(ReportWord.Number)}: {statement.Number}");
                            right.Item().AlignRight().Text($"{W(ReportWord.Issued)}: {OfficeDate(statement.IssuedAt):dd.MM.yyyy}");
                            right.Item().AlignRight().Text($"{W(ReportWord.DueBy)}: {statement.DueOn:dd.MM.yyyy}").Bold();
                        }
                    });
                });
                header.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Gold);
            });

            page.Content().Column(content =>
            {
                content.Spacing(12);
                content.Item().Column(to =>
                {
                    to.Item().Text(W(ReportWord.BilledTo).ToUpperInvariant()).FontSize(7).FontColor(Muted).LetterSpacing(0.1f);
                    to.Item().Text(partner.Name).FontSize(11).Bold();
                    if (!string.IsNullOrWhiteSpace(partner.ContactEmail))
                    {
                        to.Item().Text(partner.ContactEmail).FontColor(Muted);
                    }
                });

                content.Item().Table(table =>
                {
                    table.ColumnsDefinition(d =>
                    {
                        d.ConstantColumn(62); // date
                        d.ConstantColumn(66); // reference
                        d.RelativeColumn(2);  // passenger
                        d.RelativeColumn(3);  // route
                        d.ConstantColumn(58); // price
                        d.ConstantColumn(54); // commission
                    });
                    table.Header(h =>
                    {
                        void Head(string text, bool right = false)
                        {
                            var cell = h.Cell().BorderBottom(1).BorderColor(Ink).PaddingBottom(3).PaddingRight(4);
                            (right ? cell.AlignRight() : cell).Text(text).FontSize(7.5f).Bold();
                        }
                        Head(W(ReportWord.Date));
                        Head(W(ReportWord.Reference));
                        Head(W(ReportWord.Customer));
                        Head(W(ReportWord.Route));
                        Head(W(ReportWord.Price), true);
                        Head($"{W(ReportWord.Commission)} {statement.CommissionPercent.ToString("0.##", culture)}%", true);
                    });
                    foreach (var line in statement.Lines.OrderBy(l => l.PickupLocal))
                    {
                        var cancelled = line.Status == BookingStatus.Cancelled;
                        void Cell(string text, bool right = false)
                        {
                            var cell = table.Cell().BorderBottom(0.5f).BorderColor(Line).PaddingVertical(3).PaddingRight(4);
                            var span = (right ? cell.AlignRight() : cell).Text(text);
                            if (cancelled)
                            {
                                span.FontColor(Muted).Strikethrough();
                            }
                        }
                        Cell(line.PickupLocal.ToString("dd.MM.yy HH:mm", culture));
                        Cell(line.ExternalReference is null ? line.Reference : $"{line.Reference}\n{line.ExternalReference}");
                        Cell($"{line.Passenger}\n{t.Category(line.VehicleCategory)}");
                        Cell($"{line.From}\n→ {line.To}");
                        Cell(cancelled ? Capital(t.BookingStatusName(line.Status)) : Money(line.Price), true);
                        Cell(cancelled ? "—" : Money(line.Commission), true);
                    }
                });

                content.Item().Row(row =>
                {
                    row.RelativeItem().AlignBottom().Text(statement.Cancelled > 0 ? W(ReportWord.NotCharged) : "").FontColor(Muted);
                    row.ConstantItem(240).Column(totals =>
                    {
                        Total(totals, W(ReportWord.Rides), statement.Rides.ToString(culture));
                        if (statement.Cancelled > 0)
                        {
                            Total(totals, W(ReportWord.Cancelled), statement.Cancelled.ToString(culture));
                        }
                        Total(totals, W(ReportWord.Total), Money(statement.Revenue));
                        Total(totals, $"{W(ReportWord.Commission)} {statement.CommissionPercent.ToString("0.##", culture)}%", $"− {Money(statement.Commission)}");
                        totals.Item().PaddingTop(4).BorderTop(1).BorderColor(Gold).PaddingTop(4).Row(r =>
                        {
                            r.RelativeItem().Text(W(ReportWord.NetDue)).Bold();
                            r.AutoItem().Text(Money(statement.Net)).Bold().FontSize(11);
                        });
                    });
                });
            });

            page.Footer().Row(row =>
            {
                row.RelativeItem().Text($"{c.Name} · {W(ReportWord.Generated)} {generatedAt:dd.MM.yyyy HH:mm}").FontColor(Muted).FontSize(7.5f);
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

    private static DateTime OfficeDate(DateTimeOffset utc) => Application.Geo.OfficeTime.ToLocal(utc);

    private static void Total(ColumnDescriptor totals, string label, string value) =>
        totals.Item().PaddingVertical(1).Row(row =>
        {
            row.RelativeItem().Text(label).FontColor(Muted);
            row.AutoItem().Text(value);
        });

    private static string Capital(string text) =>
        text.Length == 0 ? text : char.ToUpper(text[0], CultureInfo.InvariantCulture) + text[1..];
}
