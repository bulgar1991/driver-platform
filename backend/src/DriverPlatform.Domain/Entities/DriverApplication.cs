using DriverPlatform.Domain.Common;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Domain.Entities;

/// <summary>A driver who wants to work with us, from the "For drivers" form on the website.</summary>
public class DriverApplication : Entity
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string City { get; set; } = "";
    public DriverLicenceKind Licence { get; set; }
    public bool HasOwnVehicle { get; set; }
    public string? Vehicle { get; set; }
    public int YearsExperience { get; set; }
    public List<string> Languages { get; set; } = [];
    public string? Message { get; set; }
    public DriverApplicationStatus Status { get; set; }
    public string? ClientIp { get; set; }
}
