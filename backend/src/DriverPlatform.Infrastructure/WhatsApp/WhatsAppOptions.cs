using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Infrastructure.WhatsApp;

public sealed class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    /// <summary>"Meta" (WhatsApp Business Cloud API) or "Log" (development: messages go to App_Data/whatsapp).</summary>
    public string Provider { get; set; } = "Log";
    public string ApiVersion { get; set; } = "v21.0";
    /// <summary>The business phone number id from Meta's WhatsApp Manager.</summary>
    public string PhoneNumberId { get; set; } = "";
    /// <summary>A permanent system-user token; keep it in secrets, never in git.</summary>
    public string AccessToken { get; set; } = "";
    /// <summary>Languages in which every template is approved; other clients get <see cref="DefaultLanguage"/>.</summary>
    public string[] Languages { get; set; } = ["it", "en", "de", "fr"];
    public string DefaultLanguage { get; set; } = "en";
    public string LogDirectory { get; set; } = "App_Data/whatsapp";

    /// <summary>Approved template name per message kind. Body parameters are listed in the README.</summary>
    public Dictionary<NotificationKind, string> Templates { get; set; } = new()
    {
        [NotificationKind.DriverShift] = "driver_shift",
        [NotificationKind.DriverShiftChanged] = "driver_shift_changed",
        [NotificationKind.ClientDriverDetails] = "client_driver_details",
        [NotificationKind.ReviewRequest] = "client_review_request",
    };

    public string LanguageFor(string language) => Languages.Contains(language) ? language : DefaultLanguage;
}
