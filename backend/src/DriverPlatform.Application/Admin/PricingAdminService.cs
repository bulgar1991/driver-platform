using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using DriverPlatform.Application.Pricing;
using DriverPlatform.Domain.Entities;
using DriverPlatform.Domain.Enums;
using DriverPlatform.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using DriverPlatform.Application.Localization;

namespace DriverPlatform.Application.Admin;

public interface IPricingAdminService
{
    Task<PricingOverview> GetAsync(CancellationToken cancellationToken);
    Task<VehicleRateDto> UpdateRateAsync(VehicleCategory category, UpdateVehicleRateRequest request, CancellationToken cancellationToken);
    Task<VehicleRateDto> CreateRateAsync(CreateVehicleRateRequest request, CancellationToken cancellationToken);
    Task<DeleteVehicleRateResult> DeleteRateAsync(VehicleCategory category, CancellationToken cancellationToken);
    Task<VehicleRateDto> SetRateActiveAsync(VehicleCategory category, bool isActive, CancellationToken cancellationToken);
    Task<IReadOnlyList<VehicleClassDto>> ClassesAsync(CancellationToken cancellationToken);
    Task<ExtraFeeDto> UpdateExtraAsync(ExtraType type, UpdateExtraFeeRequest request, CancellationToken cancellationToken);
    Task<PricingSettingsDto> UpdateSettingsAsync(PricingSettingsDto request, CancellationToken cancellationToken);
    Task<SurchargeRuleDto> CreateSurchargeAsync(SurchargeRuleRequest request, CancellationToken cancellationToken);
    Task<SurchargeRuleDto> UpdateSurchargeAsync(Guid id, SurchargeRuleRequest request, CancellationToken cancellationToken);
    Task DeleteSurchargeAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Tariffs and surcharges, editable without code changes (business plan §4). Every change is audited.</summary>
public sealed class PricingAdminService(IAppDbContext db, IAuditLogger audit) : IPricingAdminService
{
    public async Task<PricingOverview> GetAsync(CancellationToken cancellationToken)
    {
        var rates = await RatesAsync(cancellationToken);
        var surcharges = await db.SurchargeRules.AsNoTracking().ToListAsync(cancellationToken);
        var extras = (await db.ExtraFees.AsNoTracking().ToListAsync(cancellationToken)).OrderBy(f => f.Type);
        var settings = await db.LoadSettingsAsync(cancellationToken);

        return new PricingOverview(
            rates,
            surcharges.OrderBy(s => s.Kind).ThenBy(s => s.StartDate).ThenBy(s => s.Name).Select(ToDto).ToList(),
            extras.Select(f => new ExtraFeeDto(f.Type, f.Amount)).ToList(),
            ToDto(settings));
    }

    public async Task<VehicleRateDto> UpdateRateAsync(VehicleCategory category, UpdateVehicleRateRequest request, CancellationToken cancellationToken)
    {
        var rate = await db.VehicleRates.FirstOrDefaultAsync(r => r.Category == category, cancellationToken);
        if (rate is null)
        {
            // A built-in tariff missing from an old database is created; others must exist.
            if (!category.IsBuiltIn)
            {
                throw new NotFoundException(Texts.Current.TariffNotFound);
            }
            rate = new VehicleRate { Category = category, SortOrder = VehicleCategory.BuiltIn.ToList().IndexOf(category) };
            db.VehicleRates.Add(rate);
        }
        Apply(rate, request);
        if (!category.IsBuiltIn && string.IsNullOrWhiteSpace(rate.NameEn))
        {
            throw new RequestValidationException("names.en", Texts.Current.TariffNameRequired);
        }

        audit.Log("pricing.rate_updated", nameof(VehicleRate), category.ToString(), request);
        await db.SaveChangesAsync(cancellationToken);
        VehicleClasses.Set(rate);
        return ToDto(rate, await InUseAsync(category, cancellationToken));
    }

    /// <summary>A new tariff, after the others. Its code comes from the English name ("Minibus 16" → Minibus16).</summary>
    public async Task<VehicleRateDto> CreateRateAsync(CreateVehicleRateRequest request, CancellationToken cancellationToken)
    {
        var name = request.Names?.En?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new RequestValidationException("names.en", Texts.Current.TariffNameRequired);
        }
        var existing = await db.VehicleRates.ToListAsync(cancellationToken);
        var rate = new VehicleRate
        {
            Category = NewCode(name, existing.Select(r => r.Category.Code)),
            SortOrder = existing.Count == 0 ? 0 : existing.Max(r => r.SortOrder) + 1,
        };
        Apply(rate, request);
        db.VehicleRates.Add(rate);

        audit.Log("pricing.rate_created", nameof(VehicleRate), rate.Category.ToString(), request);
        await db.SaveChangesAsync(cancellationToken);
        VehicleClasses.Set(rate);
        return ToDto(rate, inUse: false);
    }

    /// <summary>
    /// Removes a tariff nobody uses. One used by bookings or cars is hidden instead (not offered for new
    /// bookings), so history, invoices and the fleet keep it.
    /// </summary>
    public async Task<DeleteVehicleRateResult> DeleteRateAsync(VehicleCategory category, CancellationToken cancellationToken)
    {
        var rate = await db.VehicleRates.FirstOrDefaultAsync(r => r.Category == category, cancellationToken)
            ?? throw new NotFoundException(Texts.Current.TariffNotFound);
        if (rate.IsActive)
        {
            await EnsureAnotherActiveAsync(category, cancellationToken);
        }

        var inUse = await InUseAsync(category, cancellationToken);
        if (inUse)
        {
            rate.IsActive = false;
            audit.Log("pricing.rate_hidden", nameof(VehicleRate), category.ToString());
        }
        else
        {
            db.VehicleRates.Remove(rate);
            // Zone prices for this class go with it.
            foreach (var zone in await db.Zones.ToListAsync(cancellationToken))
            {
                if (zone.Rates.Any(r => r.Category == category))
                {
                    zone.Rates = zone.Rates.Where(r => r.Category != category).ToList();
                }
            }
            audit.Log("pricing.rate_deleted", nameof(VehicleRate), category.ToString());
        }
        await db.SaveChangesAsync(cancellationToken);
        if (inUse)
        {
            VehicleClasses.Set(rate);
        }
        else
        {
            VehicleClasses.Remove(category);
        }
        return new DeleteVehicleRateResult(!inUse, inUse, await RatesAsync(cancellationToken));
    }

    public async Task<VehicleRateDto> SetRateActiveAsync(VehicleCategory category, bool isActive, CancellationToken cancellationToken)
    {
        var rate = await db.VehicleRates.FirstOrDefaultAsync(r => r.Category == category, cancellationToken)
            ?? throw new NotFoundException(Texts.Current.TariffNotFound);
        if (!isActive && rate.IsActive)
        {
            await EnsureAnotherActiveAsync(category, cancellationToken);
        }
        rate.IsActive = isActive;
        audit.Log(isActive ? "pricing.rate_shown" : "pricing.rate_hidden", nameof(VehicleRate), category.ToString());
        await db.SaveChangesAsync(cancellationToken);
        VehicleClasses.Set(rate);
        return ToDto(rate, await InUseAsync(category, cancellationToken));
    }

    public async Task<IReadOnlyList<VehicleClassDto>> ClassesAsync(CancellationToken cancellationToken) =>
        (await db.VehicleRates.AsNoTracking().ToListAsync(cancellationToken))
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Category)
            .Select(r => new VehicleClassDto(r.Category, Names(r), r.Category.IsBuiltIn, r.IsActive, r.MaxPassengers, r.MaxLuggage))
            .ToList();

    private async Task<IReadOnlyList<VehicleRateDto>> RatesAsync(CancellationToken cancellationToken)
    {
        var rates = (await db.VehicleRates.AsNoTracking().ToListAsync(cancellationToken)).OrderBy(r => r.SortOrder).ThenBy(r => r.Category).ToList();
        var used = (await db.Bookings.Select(b => b.VehicleCategory).Distinct().ToListAsync(cancellationToken))
            .Concat(await db.Vehicles.Select(v => v.Category).Distinct().ToListAsync(cancellationToken))
            .ToHashSet();
        return rates.Select(r => ToDto(r, used.Contains(r.Category))).ToList();
    }

    private async Task<bool> InUseAsync(VehicleCategory category, CancellationToken cancellationToken) =>
        await db.Bookings.AnyAsync(b => b.VehicleCategory == category, cancellationToken)
        || await db.Vehicles.AnyAsync(v => v.Category == category, cancellationToken);

    /// <summary>The site always offers at least one tariff.</summary>
    private async Task EnsureAnotherActiveAsync(VehicleCategory category, CancellationToken cancellationToken)
    {
        if (!await db.VehicleRates.AnyAsync(r => r.IsActive && r.Category != category, cancellationToken))
        {
            throw new ConflictException(Texts.Current.LastTariff);
        }
    }

    private static void Apply(VehicleRate rate, UpdateVehicleRateRequest request)
    {
        rate.BaseFee = request.BaseFee;
        rate.PricePerKm = request.PricePerKm;
        rate.MaxPassengers = request.MaxPassengers;
        rate.MaxLuggage = request.MaxLuggage;
        if (request.Names is { } names)
        {
            rate.NameEn = Clean(names.En);
            rate.NameIt = Clean(names.It);
            rate.NameDe = Clean(names.De);
            rate.NameFr = Clean(names.Fr);
        }
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, 60)];

    /// <summary>"Minibus 16 places" → Minibus16places; a number is added when the code is taken.</summary>
    private static VehicleCategory NewCode(string name, IEnumerable<string> taken)
    {
        var letters = new string(name.Normalize(System.Text.NormalizationForm.FormD)
            .Where(char.IsAsciiLetterOrDigit).ToArray());
        if (letters.Length == 0 || !char.IsAsciiLetter(letters[0]))
        {
            letters = "Class" + letters;
        }
        letters = char.ToUpperInvariant(letters[0]) + letters[1..];
        letters = letters[..Math.Min(letters.Length, 28)];
        var used = taken.Concat(VehicleCategory.BuiltIn.Select(c => c.Code)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var code = letters;
        for (var n = 2; used.Contains(code); n++)
        {
            code = $"{letters}{n}";
        }
        return VehicleCategory.Parse(code);
    }

    public async Task<ExtraFeeDto> UpdateExtraAsync(ExtraType type, UpdateExtraFeeRequest request, CancellationToken cancellationToken)
    {
        var fee = await db.ExtraFees.FirstOrDefaultAsync(f => f.Type == type, cancellationToken);
        if (fee is null)
        {
            fee = new ExtraFee { Type = type };
            db.ExtraFees.Add(fee);
        }
        fee.Amount = request.Amount;

        audit.Log("pricing.extra_updated", nameof(ExtraFee), type.ToString(), request);
        await db.SaveChangesAsync(cancellationToken);
        return new ExtraFeeDto(fee.Type, fee.Amount);
    }

    public async Task<PricingSettingsDto> UpdateSettingsAsync(PricingSettingsDto request, CancellationToken cancellationToken)
    {
        var settings = await db.PricingSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = new PricingSettings();
            db.PricingSettings.Add(settings);
        }
        settings.VatRatePercent = request.VatRatePercent;
        settings.ReturnDiscountPercent = request.ReturnDiscountPercent;
        settings.MinLeadTimeHours = request.MinLeadTimeHours;

        audit.Log("pricing.settings_updated", nameof(PricingSettings), settings.Id.ToString(), request);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(settings);
    }

    public async Task<SurchargeRuleDto> CreateSurchargeAsync(SurchargeRuleRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        var rule = new SurchargeRule();
        Apply(rule, request);
        db.SurchargeRules.Add(rule);

        audit.Log("pricing.surcharge_created", nameof(SurchargeRule), rule.Id.ToString(), request);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(rule);
    }

    public async Task<SurchargeRuleDto> UpdateSurchargeAsync(Guid id, SurchargeRuleRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        var rule = await db.SurchargeRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException(Texts.Current.SurchargeNotFound);
        Apply(rule, request);

        audit.Log("pricing.surcharge_updated", nameof(SurchargeRule), id.ToString(), request);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(rule);
    }

    public async Task DeleteSurchargeAsync(Guid id, CancellationToken cancellationToken)
    {
        var rule = await db.SurchargeRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException(Texts.Current.SurchargeNotFound);
        db.SurchargeRules.Remove(rule);

        audit.Log("pricing.surcharge_deleted", nameof(SurchargeRule), id.ToString(), new { rule.Name, rule.Percent });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void Validate(SurchargeRuleRequest request)
    {
        var errors = new ValidationErrors();
        switch (request.Kind)
        {
            case SurchargeKind.DaysOfWeek when request.DaysOfWeek.Count == 0:
                errors.Add("daysOfWeek", Texts.Current.PickADay);
                break;
            case SurchargeKind.DateRange when request.StartDate is null || request.EndDate is null:
                errors.Add("startDate", Texts.Current.EnterBothDates);
                break;
            case SurchargeKind.DateRange when !request.RepeatsYearly && request.EndDate < request.StartDate:
                errors.Add("endDate", Texts.Current.EndBeforeStart);
                break;
            case SurchargeKind.TimeOfDay when request.StartTime is null || request.EndTime is null:
                errors.Add("startTime", Texts.Current.EnterBothTimes);
                break;
            case SurchargeKind.TimeOfDay when request.StartTime == request.EndTime:
                errors.Add("endTime", Texts.Current.SameStartAndEnd);
                break;
        }
        errors.ThrowIfAny();
    }

    /// <summary>Only the fields of the chosen kind are kept, so a rule never matches on stale values.</summary>
    private static void Apply(SurchargeRule rule, SurchargeRuleRequest request)
    {
        rule.Name = request.Name.Trim();
        rule.NameTranslations = request.NameTranslations.Cleaned();
        rule.Kind = request.Kind;
        rule.Percent = request.Percent;
        rule.IsActive = request.IsActive;
        rule.DaysOfWeek = request.Kind == SurchargeKind.DaysOfWeek ? request.DaysOfWeek.Distinct().Order().ToList() : [];
        rule.StartDate = request.Kind == SurchargeKind.DateRange ? request.StartDate : null;
        rule.EndDate = request.Kind == SurchargeKind.DateRange ? request.EndDate : null;
        rule.RepeatsYearly = request.Kind == SurchargeKind.DateRange && request.RepeatsYearly;
        rule.StartTime = request.Kind == SurchargeKind.TimeOfDay ? request.StartTime : null;
        rule.EndTime = request.Kind == SurchargeKind.TimeOfDay ? request.EndTime : null;
    }

    private static VehicleRateDto ToDto(VehicleRate r, bool inUse) => new(
        r.Category, r.BaseFee, r.PricePerKm, r.MaxPassengers, r.MaxLuggage, Names(r), r.Category.IsBuiltIn, r.IsActive, r.SortOrder, inUse);

    private static VehicleClassNames Names(VehicleRate r) => new(r.NameEn, r.NameIt, r.NameDe, r.NameFr);

    private static SurchargeRuleDto ToDto(SurchargeRule r) => new(
        r.Id, r.Name, r.NameTranslations ?? new Translations(), r.Kind, r.Percent, r.DaysOfWeek, r.StartDate, r.EndDate, r.StartTime, r.EndTime, r.RepeatsYearly, r.IsActive);

    private static PricingSettingsDto ToDto(PricingSettings s) => new()
    {
        VatRatePercent = s.VatRatePercent,
        ReturnDiscountPercent = s.ReturnDiscountPercent,
        MinLeadTimeHours = s.MinLeadTimeHours,
    };
}
