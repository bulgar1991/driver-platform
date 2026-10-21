using System.Globalization;
using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Geo;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DriverPlatform.Infrastructure.Documents;

/// <summary>
/// A4 invoice / credit note in Italian and English. This is the PDF copy for the client; the legal
/// electronic invoice (FatturaPA via SdI) is a separate step to set up with the accountant.
/// </summary>
public sealed class QuestInvoiceRenderer : IInvoiceRenderer
{
    private const string Ink = "#0B0B0C";
    private const string Gold = "#A8873F";
    private const string Muted = "#6B6B6B";
    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");

    static QuestInvoiceRenderer()
    {
        // Free for companies under USD 1M annual revenue; see questpdf.com/license.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] RenderPdf(Invoice invoice, string bookingReference, string? originalNumber)
    {
        var creditNote = invoice.Type == InvoiceType.CreditNote;
        var title = creditNote ? "NOTA DI CREDITO · CREDIT NOTE" : "FATTURA · INVOICE";
        var issued = ItalyTime.ToLocal(invoice.IssuedAt).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.DefaultTextStyle(t => t.FontSize(9.5f).FontColor(Ink));

            page.Header().Column(header =>
            {
                header.Item().Row(row =>
                {
                    row.RelativeItem().Column(seller =>
                    {
                        seller.Item().Text(invoice.Seller.Name.ToUpperInvariant()).FontSize(18).LetterSpacing(0.15f).Bold();
                        Party(seller, invoice.Seller);
                    });
                    row.ConstantItem(200).AlignRight().Column(meta =>
                    {
                        meta.Item().AlignRight().Text(title).FontSize(11).Bold().FontColor(Gold);
                        meta.Item().AlignRight().Text($"N. {invoice.Number}").FontSize(11).Bold();
                        meta.Item().AlignRight().Text($"Data / Date: {issued}");
                        meta.Item().AlignRight().Text($"Prenotazione / Booking: {bookingReference}");
                        if (originalNumber is not null)
                        {
                            meta.Item().AlignRight().Text($"Rif. fattura / Invoice: {originalNumber}");
                        }
                    });
                });
                header.Item().PaddingVertical(12).LineHorizontal(1).LineColor(Gold);
            });

            page.Content().Column(content =>
            {
                content.Spacing(14);
                content.Item().Column(buyer =>
                {
                    buyer.Item().Text("Cliente / Bill to").FontColor(Muted).FontSize(8);
                    buyer.Item().Text(invoice.Buyer.Name).Bold();
                    Party(buyer, invoice.Buyer);
                });

                content.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(6);
                        c.ConstantColumn(30);
                        c.ConstantColumn(45);
                        c.ConstantColumn(65);
                        c.ConstantColumn(55);
                        c.ConstantColumn(65);
                    });
                    table.Header(h =>
                    {
                        string[] labels = ["Descrizione / Description", "Q.tà", "IVA", "Imponibile", "IVA €", "Totale"];
                        for (var i = 0; i < labels.Length; i++)
                        {
                            var cell = h.Cell().BorderBottom(1).BorderColor(Ink).PaddingBottom(4);
                            // Number columns are right-aligned like their values.
                            (i == 0 ? cell : cell.AlignRight()).Text(labels[i]).FontSize(8).Bold();
                        }
                    });
                    foreach (var line in invoice.Lines)
                    {
                        Cell(table).Text(line.Description);
                        Cell(table).AlignRight().Text(line.Quantity.ToString(CultureInfo.InvariantCulture));
                        Cell(table).AlignRight().Text($"{line.VatRatePercent.ToString("0.##", Italian)}%");
                        Cell(table).AlignRight().Text(Money(line.NetAmount));
                        Cell(table).AlignRight().Text(Money(line.VatAmount));
                        Cell(table).AlignRight().Text(Money(line.TotalAmount));
                    }
                });

                content.Item().AlignRight().Width(230).Column(totals =>
                {
                    Total(totals, "Imponibile / Net", invoice.NetAmount);
                    foreach (var group in invoice.Lines.GroupBy(l => l.VatRatePercent))
                    {
                        Total(totals, $"IVA / VAT {group.Key.ToString("0.##", Italian)}%", group.Sum(l => l.VatAmount));
                    }
                    totals.Item().PaddingTop(4).BorderTop(1).BorderColor(Gold).PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem().Text(creditNote ? "Totale rimborsato / Refunded" : "Totale / Total").Bold();
                        row.AutoItem().Text(Money(invoice.TotalAmount)).Bold().FontSize(12);
                    });
                });

                content.Item().Text(creditNote
                        ? "Importo rimborsato sul metodo di pagamento originale. / Refunded to the original payment method."
                        : "Pagato online. / Paid online.")
                    .FontColor(Muted);
            });

            page.Footer().AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(t => t.FontSize(7.5f).FontColor(Muted));
                text.Span($"{invoice.Seller.Name} · ");
                text.Span("Pagina ");
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        })).GeneratePdf();
    }

    private static void Party(ColumnDescriptor column, InvoiceParty party)
    {
        foreach (var line in new[]
                 {
                     party.Address,
                     party.VatNumber is null ? null : $"P.IVA {party.VatNumber}",
                     party.TaxCode is null ? null : $"C.F. {party.TaxCode}",
                     party.Email,
                     party.Phone,
                 }.Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            column.Item().Text(line!).FontColor(Muted);
        }
    }

    private static IContainer Cell(TableDescriptor table) =>
        table.Cell().BorderBottom(0.5f).BorderColor("#DDDDDD").PaddingVertical(5);

    private static void Total(ColumnDescriptor column, string label, decimal amount) =>
        column.Item().Row(row =>
        {
            row.RelativeItem().Text(label).FontColor(Muted);
            row.AutoItem().Text(Money(amount));
        });

    private static string Money(decimal amount) => amount.ToString("€ #,##0.00", Italian);
}
