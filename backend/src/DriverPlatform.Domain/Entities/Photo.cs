using DriverPlatform.Domain.Common;

namespace DriverPlatform.Domain.Entities;

/// <summary>A vehicle or driver picture. Never changed: a new upload is a new row, so its URL can be cached forever.</summary>
public class Photo : Entity
{
    public string ContentType { get; set; } = "";
    public byte[] Data { get; set; } = [];
}
