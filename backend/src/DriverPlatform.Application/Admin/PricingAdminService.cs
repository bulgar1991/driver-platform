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
        // Enums are stored as text; sort by enum value in memory.
        var rates = (await db.VehicleRates.AsNoTracking().ToListAsync(cancellationToken)).OrderBy(r => r.Category);
        var surcharges = await db.SurchargeRules.AsNoTracking().ToListAsync(cancellationToken);
        var extras = (await db.ExtraFees.AsNoTracking().ToListAsync(cancellationToken)).OrderBy(f => f.Type);
        var settings = await db.LoadSettingsAsync(cancellationToken);

        return new PricingOverview(
            rates.Select(ToDto).ToList(),
            surcharges.OrderBy(s => s.Kind).ThenBy(s => s.StartDate).ThenBy(s => s.Name).Select(ToDto).ToList(),
            extras.Select(f => new ExtraFeeDto(f.Type, f.Amount)).ToList(),
            ToDto(settings));
    }

    public async Task<VehicleRateDto> UpdateRateAsync(VehicleCategory category, UpdateVehicleRateRequest request, CancellationToken cancellationToken)
    {
        var rate = await db.VehicleRates.FirstOrDefaultAsync(r => r.Category == category, cancellationToken);
        if (rate is null)
        {
            rate = new VehicleRate { Category = category };
            db.VehicleRates.Add(rate);
        }
        rate.BaseFee = request.BaseFee;
        rate.PricePerKm = request.PricePerKm;
        rate.MaxPassengers = request.MaxPassengers;
        rate.MaxLuggage = request.MaxLuggage;

        audit.Log("pricing.rate_updated", nameof(VehicleRate), category.ToString(), request);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(rate);
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

    private static VehicleRateDto ToDto(VehicleRate r) => new(r.Category, r.BaseFee, r.PricePerKm, r.MaxPassengers, r.MaxLuggage);

    private static SurchargeRuleDto ToDto(SurchargeRule r) => new(
        r.Id, r.Name, r.NameTranslations ?? new Translations(), r.Kind, r.Percent, r.DaysOfWeek, r.StartDate, r.EndDate, r.StartTime, r.EndTime, r.RepeatsYearly, r.IsActive);

    private static PricingSettingsDto ToDto(PricingSettings s) => new()
    {
        VatRatePercent = s.VatRatePercent,
        ReturnDiscountPercent = s.ReturnDiscountPercent,
        MinLeadTimeHours = s.MinLeadTimeHours,
    };
}
