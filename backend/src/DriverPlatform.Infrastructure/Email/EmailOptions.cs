namespace DriverPlatform.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "Magnus Drive";
    /// <summary>When set, emails are written as .eml files to this folder instead of being sent (development).</summary>
    public string? PickupDirectory { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}
