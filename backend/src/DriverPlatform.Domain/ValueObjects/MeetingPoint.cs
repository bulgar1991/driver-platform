namespace DriverPlatform.Domain.ValueObjects;

/// <summary>A place reachable by car where the client is picked up or dropped off.</summary>
public sealed class MeetingPoint
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    /// <summary>In English; clients read <see cref="ClientInstructionsTranslations"/> in their language.</summary>
    public string? ClientInstructions { get; set; }
    public Translations? ClientInstructionsTranslations { get; set; }
    public string? DriverInstructions { get; set; }
}
