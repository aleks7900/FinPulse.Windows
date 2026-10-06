using System;
using System.Text.Json.Serialization;

namespace FinPulse.Windows.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentFrequency
{
    DAILY,
    WEEKLY,
    BI_WEEKLY,
    MONTHLY,
    QUARTERLY,
    YEARLY,
    CUSTOM
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CustomIntervalUnit
{
    DAYS,
    WEEKS,
    MONTHS,
    YEARS
}

public class RecurringTransaction
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public Money Amount { get; set; } = Money.Zero("USD");

    [JsonPropertyName("type")]
    public TransactionType Type { get; set; } = TransactionType.EXPENSE;

    [JsonPropertyName("accountId")]
    public string AccountId { get; set; } = string.Empty;

    [JsonPropertyName("destinationAccountId")]
    public string? DestinationAccountId { get; set; }

    [JsonPropertyName("categoryId")]
    public string CategoryId { get; set; } = string.Empty;

    [JsonPropertyName("frequency")]
    public PaymentFrequency Frequency { get; set; } = PaymentFrequency.MONTHLY;

    [JsonPropertyName("customIntervalValue")]
    public int CustomIntervalValue { get; set; } = 1;

    [JsonPropertyName("customIntervalUnit")]
    public CustomIntervalUnit CustomIntervalUnit { get; set; } = CustomIntervalUnit.MONTHS;

    [JsonPropertyName("anchorDayOfMonth")]
    public int AnchorDayOfMonth { get; set; } = 1;

    [JsonPropertyName("nextDueDate")]
    public long NextDueDate { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    [JsonPropertyName("lastProcessedDate")]
    public long? LastProcessedDate { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("isCancelled")]
    public bool IsCancelled { get; set; } = false;

    [JsonPropertyName("isSubscription")]
    public bool IsSubscription { get; set; } = false;

    [JsonPropertyName("isVariableAmount")]
    public bool IsVariableAmount { get; set; } = false;

    [JsonPropertyName("reminderDaysBefore")]
    public int ReminderDaysBefore { get; set; } = 1;

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("createdAt")]
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    [JsonPropertyName("updatedAt")]
    public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    [JsonIgnore]
    public DateTime NextDueDateTime => DateTimeOffset.FromUnixTimeMilliseconds(NextDueDate).LocalDateTime;

    [JsonIgnore]
    public string FormattedNextDue => NextDueDateTime.ToString("d", System.Globalization.CultureInfo.CurrentCulture);

    [JsonIgnore]
    public string CategoryName { get; set; } = string.Empty;

    [JsonIgnore]
    public string AccountName { get; set; } = string.Empty;

    [JsonIgnore]
    public string FrequencyDisplayName => Frequency switch
    {
        PaymentFrequency.DAILY => FinPulse.Windows.Services.LocalizationService.Current.GetString("Frequency_Daily"),
        PaymentFrequency.WEEKLY => FinPulse.Windows.Services.LocalizationService.Current.GetString("Frequency_Weekly"),
        PaymentFrequency.BI_WEEKLY => FinPulse.Windows.Services.LocalizationService.Current.GetString("Frequency_BiWeekly"),
        PaymentFrequency.MONTHLY => FinPulse.Windows.Services.LocalizationService.Current.GetString("Frequency_Monthly"),
        PaymentFrequency.QUARTERLY => FinPulse.Windows.Services.LocalizationService.Current.GetString("Frequency_Quarterly"),
        PaymentFrequency.YEARLY => FinPulse.Windows.Services.LocalizationService.Current.GetString("Frequency_Yearly"),
        _ => FinPulse.Windows.Services.LocalizationService.Current.GetString("Frequency_Custom")
    };

    public Money CalculateMonthlyCost()
    {
        double multiplier = Frequency switch
        {
            PaymentFrequency.DAILY => 30.0,
            PaymentFrequency.WEEKLY => 52.0 / 12.0,
            PaymentFrequency.BI_WEEKLY => 26.0 / 12.0,
            PaymentFrequency.MONTHLY => 1.0,
            PaymentFrequency.QUARTERLY => 1.0 / 3.0,
            PaymentFrequency.YEARLY => 1.0 / 12.0,
            _ => 1.0
        };
        return Amount * multiplier;
    }

    [JsonIgnore]
    public string FormattedAmount => Amount.FormattedString;
}
