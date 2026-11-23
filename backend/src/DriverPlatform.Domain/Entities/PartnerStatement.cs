using DriverPlatform.Domain.Common;
using DriverPlatform.Domain.Enums;

namespace DriverPlatform.Domain.Entities;

/// <summary>
/// A partner's monthly statement, once issued: the rides and amounts are frozen with the PDF, so what the
/// partner received never changes. Issuing again (before it is paid) keeps the number and replaces the copy.
/// </summary>
public class PartnerStatement : Entity
{
    public Guid PartnerId { get; set; }
    public Partner Partner { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    /// <summary>One sequence per year, e.g. "MD-ST-2026-0001".</summary>
    public string Number { get; set; } = "";
    public int Sequence { get; set; }

    public int Rides { get; set; }
    public int Cancelled { get; set; }
    /// <summary>Price of the rides not cancelled.</summary>
    public decimal Revenue { get; set; }
    public decimal CommissionPercent { get; set; }
    public decimal Commission { get; set; }
    /// <summary>What the partner pays: revenue less commission.</summary>
    public decimal Net { get; set; }
    public string Currency { get; set; } = "EUR";
    public List<PartnerStatementLine> Lines { get; set; } = [];
    public byte[]? Pdf { get; set; }

    public DateTimeOffset IssuedAt { get; set; }
    public DateOnly DueOn { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public string? SentTo { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public string? Note { get; set; }
}

/// <summary>One ride as it stood when the statement was issued.</summary>
public class PartnerStatementLine
{
    public Guid BookingId { get; set; }
    public string Reference { get; set; } = "";
    public string? ExternalReference { get; set; }
    /// <summary>Local time at the pickup.</summary>
    public DateTime PickupLocal { get; set; }
    public string Passenger { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public VehicleCategory VehicleCategory { get; set; }
    public BookingStatus Status { get; set; }
    /// <summary>Charged: zero for a cancelled ride.</summary>
    public decimal Price { get; set; }
    public decimal Commission { get; set; }
}
