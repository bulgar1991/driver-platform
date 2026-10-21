using DriverPlatform.Domain.Entities;

namespace DriverPlatform.Application.Abstractions;

public interface IInvoiceRenderer
{
    /// <param name="bookingReference">Printed on the document so the client can match it to the ride.</param>
    byte[] RenderPdf(Invoice invoice, string bookingReference, string? originalNumber);
}
