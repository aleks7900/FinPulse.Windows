using System;
using System.Text.Json.Serialization;

namespace FinPulse.Windows.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BudgetPeriod
{
    WEEKLY,
    MONTHLY,
    CUSTOM
}

public class Budget
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("categoryId")]
    public string CategoryId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("limitAmount")]
    public Money LimitAmount { get; set; } = Money.Zero("USD");

    [JsonPropertyName("periodType")]
    public BudgetPeriod PeriodType { get; set; } = BudgetPeriod.MONTHLY;

    [JsonPropertyName("startDate")]
    public long StartDate { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    [JsonPropertyName("endDate")]
    public long EndDate { get; set; } = DateTimeOffset.UtcNow.AddMonths(1).ToUnixTimeMilliseconds();

    [JsonPropertyName("notifyAt70")]
    public bool NotifyAt70 { get; set; } = true;

    [JsonPropertyName("notifyAt90")]
    public bool NotifyAt90 { get; set; } = true;

    [JsonPropertyName("notifyAt100")]
    public bool NotifyAt100 { get; set; } = true;

    [JsonPropertyName("isArchived")]
    public bool IsArchived { get; set; } = false;

    [JsonPropertyName("isOverall")]
    public bool IsOverall { get; set; } = false;

    [JsonPropertyName("isRolloverEnabled")]
    public bool IsRolloverEnabled { get; set; } = false;

    [JsonPropertyName("rolloverAmountMinor")]
    public long RolloverAmountMinor { get; set; } = 0L;

    [JsonPropertyName("alertThresholdPercent")]
    public int AlertThresholdPercent { get; set; } = 85;

    [JsonPropertyName("createdAt")]
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    [JsonPropertyName("updatedAt")]
    public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    [JsonIgnore]
    public Money EffectiveLimit
    {
        get
        {
            long total = IsRolloverEnabled ? LimitAmount.AmountMinor + RolloverAmountMinor : LimitAmount.AmountMinor;
            return new Money(Math.Max(0, total), LimitAmount.CurrencyCode);
        }
    }

    // Calculated desktop properties
    [JsonIgnore]
    public Money SpentAmount { get; set; } = Money.Zero("USD");

    [JsonIgnore]
    public Money RemainingAmount
    {
        get
        {
            long rem = EffectiveLimit.AmountMinor - SpentAmount.AmountMinor;
            return new Money(Math.Max(0, rem), EffectiveLimit.CurrencyCode);
        }
    }

    [JsonIgnore]
    public double ConsumedPercentage
    {
        get
        {
            if (EffectiveLimit.AmountMinor <= 0) return 0.0;
            return Math.Clamp((double)SpentAmount.AmountMinor / EffectiveLimit.AmountMinor, 0.0, 2.0);
        }
    }

    [JsonIgnore]
    public int ConsumedPercentInt => (int)Math.Round(ConsumedPercentage * 100);

    [JsonIgnore]
    public string CategoryName { get; set; } = string.Empty;

    [JsonIgnore]
    public string StatusText => ConsumedPercentage switch
    {
        >= 1.0 => FinPulse.Windows.Services.LocalizationService.Current.GetString("Budget_Status_Exceeded"),
        >= 0.85 => FinPulse.Windows.Services.LocalizationService.Current.GetString("Budget_Status_NearLimit"),
        _ => FinPulse.Windows.Services.LocalizationService.Current.GetString("Budget_Status_OnTrack")
    };

    [JsonIgnore]
    public string StatusColor => ConsumedPercentage switch
    {
        >= 1.0 => "#D32F2F", // Red
        >= 0.85 => "#FFA000", // Amber
        _ => "#2E7D32" // Green
    };

    [JsonIgnore]
    public string FormattedSpent => SpentAmount.FormattedString;

    [JsonIgnore]
    public string FormattedEffectiveLimit => EffectiveLimit.FormattedString;

    [JsonIgnore]
    public string FormattedRemaining => RemainingAmount.FormattedString;

    [JsonIgnore]
    public string FormattedLimit => LimitAmount.FormattedString;

    [JsonIgnore]
    public string ConsumedPercentString => $"{ConsumedPercentInt}%";
}
