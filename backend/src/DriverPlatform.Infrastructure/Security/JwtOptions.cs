namespace DriverPlatform.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "magnus-drive";
    public string Audience { get; set; } = "magnus-drive";
    /// <summary>At least 32 characters. Set through configuration, never committed.</summary>
    public string SigningKey { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 480;
}
