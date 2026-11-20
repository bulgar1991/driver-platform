using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DriverPlatform.Domain.Enums;

/// <summary>
/// A vehicle tariff class, by its code. Four come with the platform (Sedan, Business, Estate, Van:
/// shown as Standard, Business, VIP, Bus); the admin adds more in Admin → Pricing ("Minibus16"…).
/// Stored and sent as the plain code, exactly like the enum it replaced, so the database, the
/// partner API and Booking.com need nothing new.
/// </summary>
[JsonConverter(typeof(VehicleCategoryJsonConverter))]
[TypeConverter(typeof(VehicleCategoryTypeConverter))]
public readonly partial record struct VehicleCategory : IComparable<VehicleCategory>, IComparable
{
    public static readonly VehicleCategory Sedan = new("Sedan");
    public static readonly VehicleCategory Business = new("Business");
    public static readonly VehicleCategory Estate = new("Estate");
    public static readonly VehicleCategory Van = new("Van");

    /// <summary>The platform's own classes, cheapest first.</summary>
    public static readonly IReadOnlyList<VehicleCategory> BuiltIn = [Sedan, Business, Estate, Van];

    private VehicleCategory(string code) => Code = code;

    /// <summary>Letters and digits, starting with a letter, at most 32.</summary>
    public string Code { get; }

    public bool IsBuiltIn => BuiltIn.Contains(this);

    public static bool TryParse(string? value, out VehicleCategory category)
    {
        var text = value?.Trim() ?? "";
        if (!CodePattern().IsMatch(text))
        {
            category = default;
            return false;
        }
        // "sedan" and "SEDAN" are the built-in class, as with the old enum.
        category = BuiltIn.FirstOrDefault(c => string.Equals(c.Code, text, StringComparison.OrdinalIgnoreCase)) is { Code: not null } builtIn
            ? builtIn
            : new VehicleCategory(text);
        return true;
    }

    public static VehicleCategory Parse(string value) =>
        TryParse(value, out var category) ? category : throw new FormatException($"Not a vehicle class code: \"{value}\".");

    public override string ToString() => Code ?? "";

    /// <summary>Built-in classes first, cheapest first; then the others by code.</summary>
    public int CompareTo(VehicleCategory other)
    {
        var (a, b) = (Rank(this), Rank(other));
        return a != b ? a.CompareTo(b) : string.CompareOrdinal(Code, other.Code);
    }

    int IComparable.CompareTo(object? obj) => obj is VehicleCategory other ? CompareTo(other) : 1;

    private static int Rank(VehicleCategory category)
    {
        for (var i = 0; i < BuiltIn.Count; i++)
        {
            if (BuiltIn[i] == category)
            {
                return i;
            }
        }
        return BuiltIn.Count;
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9]{0,31}$")]
    private static partial Regex CodePattern();
}

public sealed class VehicleCategoryJsonConverter : JsonConverter<VehicleCategory>
{
    public override VehicleCategory Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String && VehicleCategory.TryParse(reader.GetString(), out var category)
            ? category
            : throw new JsonException("Not a vehicle class code.");

    public override void Write(Utf8JsonWriter writer, VehicleCategory value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Code);

    // As a dictionary key: { "Sedan": … }.
    public override VehicleCategory ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        VehicleCategory.TryParse(reader.GetString(), out var category) ? category : throw new JsonException("Not a vehicle class code.");

    public override void WriteAsPropertyName(Utf8JsonWriter writer, VehicleCategory value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value.Code);
}

/// <summary>For route and query values: /api/admin/pricing/rates/Sedan.</summary>
public sealed class VehicleCategoryTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string text ? VehicleCategory.Parse(text) : base.ConvertFrom(context, culture, value);
}
