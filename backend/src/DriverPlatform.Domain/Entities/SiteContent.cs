using DriverPlatform.Domain.Common;

namespace DriverPlatform.Domain.Entities;

/// <summary>
/// One piece of the public website changed in the panel: a text in the four languages and/or a
/// picture. Keys are named by the site ("welcome.title", "vehicle.van.image"); a key without a row
/// shows the site's built-in text or picture.
/// </summary>
public class SiteContent : Entity
{
    public string Key { get; set; } = "";
    public string? En { get; set; }
    public string? It { get; set; }
    public string? De { get; set; }
    public string? Fr { get; set; }
    public Guid? PhotoId { get; set; }
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
}
