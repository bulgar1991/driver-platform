using DriverPlatform.Application.Search;

namespace DriverPlatform.Application.Abstractions;

/// <summary>Turns a search into a printable PDF report.</summary>
public interface IReportRenderer
{
    byte[] Render(ReportRequest report);
}
