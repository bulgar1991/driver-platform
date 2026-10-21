namespace DriverPlatform.Application.Quotes;

public interface IQuoteService
{
    Task<QuoteResponse> QuoteAsync(QuoteRequest request, CancellationToken cancellationToken);
    Task<BookingOptionsResponse> GetOptionsAsync(CancellationToken cancellationToken);
}
