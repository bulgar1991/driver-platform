using System.Collections.Concurrent;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Application.Pricing;

/// <summary>
/// Names of the vehicle classes as set in Admin → Pricing, for texts made outside a request that
/// loads the tariffs (price lines, emails, invoices). Loaded at startup and updated on every change
/// (one server per client).
/// </summary>
public static class VehicleClasses
{
    /// <summary>The platform's own names: the same in every language.</summary>
    private static readonly Dictionary<VehicleCategory, string> BuiltInNames = new()
    {
        [VehicleCategory.Sedan] = "Standard",
        [VehicleCategory.Business] = "Business",
        [VehicleCategory.Estate] = "VIP",
        [VehicleCategory.Van] = "Bus",
    };

    private static readonly ConcurrentDictionary<VehicleCategory, VehicleRate> Rates = new();

    public static void Load(IEnumerable<VehicleRate> rates)
    {
        Rates.Clear();
        foreach (var rate in rates)
        {
            Set(rate);
        }
    }

    public static void Set(VehicleRate rate) => Rates[rate.Category] = rate;

    public static void Remove(VehicleCategory category) => Rates.TryRemove(category, out _);

    /// <summary>The name in a language: the admin's name for it, the built-in name, or the code.</summary>
    public static string Name(VehicleCategory category, string language)
    {
        if (Rates.TryGetValue(category, out var rate) && rate.Name(language) is { } custom)
        {
            return custom;
        }
        return BuiltInNames.TryGetValue(category, out var builtIn) ? builtIn : category.Code;
    }

    /// <summary>
    /// A car of one class can drive a ride booked in another: the same class, or a bigger built-in one
    /// (a Bus can do a Standard ride). Classes added in the panel only drive their own rides.
    /// </summary>
    public static bool CanServe(VehicleCategory car, VehicleCategory booked)
    {
        if (car == booked)
        {
            return true;
        }
        var carRank = IndexOf(car);
        var bookedRank = IndexOf(booked);
        return carRank >= 0 && bookedRank >= 0 && carRank > bookedRank;
    }

    private static int IndexOf(VehicleCategory category)
    {
        for (var i = 0; i < VehicleCategory.BuiltIn.Count; i++)
        {
            if (VehicleCategory.BuiltIn[i] == category)
            {
                return i;
            }
        }
        return -1;
    }
}
