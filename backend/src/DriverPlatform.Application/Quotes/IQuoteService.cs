namespace DriverPlatform.Application.Quotes;

public interface IQuoteService
{
    Task<QuoteResponse> QuoteAsync(QuoteRequest request, CancellationToken cancellationToken);
    /// <summary>For the office: no minimum notice, and rides up to a month back can be entered afterwards.</summary>
    Task<QuoteResponse> QuoteForStaffAsync(QuoteRequest request, CancellationToken cancellationToken);
    Task<BookingOptionsResponse> GetOptionsAsync(CancellationToken cancellationToken);
}
