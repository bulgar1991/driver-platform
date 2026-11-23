using DriverPlatform.Domain.Entities;

namespace DriverPlatform.Application.Abstractions;

/// <summary>The partner statement as a PDF. <paramref name="statement"/> has no number yet for a draft.</summary>
public interface IStatementRenderer
{
    byte[] Render(PartnerStatement statement, Partner partner, bool draft, DateTime generatedAt);
}
