using DriverPlatform.Domain.Common;

namespace DriverPlatform.Domain.Entities;

/// <summary>Single row of global pricing settings, edited in the admin panel.</summary>
public class PricingSettings : Entity
{
    /// <summary>VAT included in every price. To confirm with the accountant.</summary>
    public decimal VatRatePercent { get; set; }
    /// <summary>Discount on the return leg when both legs are booked together.</summary>
    public decimal ReturnDiscountPercent { get; set; }
    /// <summary>Earliest a ride can be booked, counted from now.</summary>
    public int MinLeadTimeHours { get; set; }
}
