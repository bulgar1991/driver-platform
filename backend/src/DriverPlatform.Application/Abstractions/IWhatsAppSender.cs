using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Application.Abstractions;

/// <summary>
/// WhatsApp Business messages. Messages we start must use templates approved by Meta, so each
/// notification kind maps to a template whose {{1}}, {{2}}… are filled with <c>parameters</c>.
/// </summary>
public interface IWhatsAppSender
{
    /// <param name="phone">E.164, e.g. "+393331234567".</param>
    /// <returns>The provider's message id.</returns>
    Task<string?> SendTemplateAsync(
        string phone, NotificationKind template, string language, IReadOnlyList<string> parameters, CancellationToken cancellationToken);
}
