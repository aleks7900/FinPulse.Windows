using System;
using System.Text.Json.Serialization;

namespace FinPulse.Windows.Models;

public class FinancialGoal
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("targetAmount")]
    public Money TargetAmount { get; set; } = Money.Zero("USD");

    [JsonPropertyName("currentAmount")]
    public Money CurrentAmount { get; set; } = Money.Zero("USD");

    [JsonPropertyName("targetDate")]
    public long TargetDate { get; set; } = DateTimeOffset.UtcNow.AddMonths(6).ToUnixTimeMilliseconds();

    [JsonPropertyName("linkedAccountId")]
    public string? LinkedAccountId { get; set; }

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "savings";

    [JsonPropertyName("colorHex")]
    public long ColorHex { get; set; } = 0xFF4CAF50;

    [JsonPropertyName("isCompleted")]
    public bool IsCompleted { get; set; } = false;

    [JsonPropertyName("createdAt")]
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    [JsonIgnore]
    public double ProgressPercentage
    {
        get
        {
            if (TargetAmount.AmountMinor <= 0) return 0.0;
            return Math.Clamp((double)CurrentAmount.AmountMinor / TargetAmount.AmountMinor, 0.0, 1.0);
        }
    }

    [JsonIgnore]
    public int ProgressPercentInt => (int)Math.Round(ProgressPercentage * 100);

    [JsonIgnore]
    public Money RemainingAmount
    {
        get
        {
            long rem = Math.Max(0, TargetAmount.AmountMinor - CurrentAmount.AmountMinor);
            return new Money(rem, TargetAmount.CurrencyCode);
        }
    }

    [JsonIgnore]
    public DateTime TargetDateTime => DateTimeOffset.FromUnixTimeMilliseconds(TargetDate).LocalDateTime;

    [JsonIgnore]
    public string FormattedTargetDate => TargetDateTime.ToString("d", System.Globalization.CultureInfo.CurrentCulture);

    [JsonIgnore]
    public string HexColorString => $"#{((uint)ColorHex):X8}";

    public Money CalculateSuggestedMonthlyContribution()
    {
        if (IsCompleted || CurrentAmount >= TargetAmount)
            return Money.Zero(TargetAmount.CurrencyCode);

        var now = DateTime.UtcNow;
        var target = DateTimeOffset.FromUnixTimeMilliseconds(TargetDate).UtcDateTime;

        int months = Math.Max(1, ((target.Year - now.Year) * 12) + target.Month - now.Month);
        return RemainingAmount / months;
    }

    [JsonIgnore]
    public string FormattedTargetAmount => TargetAmount.FormattedString;

    [JsonIgnore]
    public string FormattedCurrentAmount => CurrentAmount.FormattedString;

    [JsonIgnore]
    public string FormattedRemainingAmount => RemainingAmount.FormattedString;

    [JsonIgnore]
    public string FormattedSuggestedMonthlyContribution => CalculateSuggestedMonthlyContribution().FormattedString;

    [JsonIgnore]
    public string ProgressPercentString => $"{ProgressPercentInt}%";
}
