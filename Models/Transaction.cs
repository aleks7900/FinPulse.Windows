using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FinPulse.Windows.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TransactionType
{
    INCOME,
    EXPENSE,
    TRANSFER,
    REFUND
}

public class Transaction
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("amount")]
    public Money Amount { get; set; } = Money.Zero("USD");

    [JsonPropertyName("type")]
    public TransactionType Type { get; set; } = TransactionType.EXPENSE;

    [JsonPropertyName("sourceAccountId")]
    public string SourceAccountId { get; set; } = string.Empty;

    [JsonPropertyName("destinationAccountId")]
    public string? DestinationAccountId { get; set; }

    [JsonPropertyName("categoryId")]
    public string CategoryId { get; set; } = string.Empty;

    [JsonPropertyName("merchant")]
    public string? Merchant { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("recurringRuleId")]
    public string? RecurringRuleId { get; set; }

    [JsonPropertyName("isExcludedFromBudget")]
    public bool IsExcludedFromBudget { get; set; } = false;

    [JsonPropertyName("isCategoryConfirmed")]
    public bool IsCategoryConfirmed { get; set; } = true;

    [JsonPropertyName("categorizationConfidence")]
    public float CategorizationConfidence { get; set; } = 1.0f;

    [JsonPropertyName("matchedRuleId")]
    public string? MatchedRuleId { get; set; }

    [JsonPropertyName("createdAt")]
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    [JsonPropertyName("exchangeRate")]
    public double? ExchangeRate { get; set; }

    [JsonPropertyName("exchangeRateDate")]
    public long? ExchangeRateDate { get; set; }

    [JsonPropertyName("destinationAmount")]
    public Money? DestinationAmount { get; set; }

    // Desktop UI helper properties (Ignored during JSON sync to Firestore)
    [JsonIgnore]
    public DateTime DateTime => DateTimeOffset.FromUnixTimeMilliseconds(Timestamp).LocalDateTime;

    [JsonIgnore]
    public string FormattedDate => DateTime.ToString("MMM dd, yyyy");

    [JsonIgnore]
    public string FormattedTime => DateTime.ToString("HH:mm");

    [JsonIgnore]
    public string DisplayTitle => !string.IsNullOrWhiteSpace(Merchant)
        ? Merchant
        : (!string.IsNullOrWhiteSpace(Description) ? Description : CategoryName);

    [JsonIgnore]
    public string DisplaySubtitle => !string.IsNullOrWhiteSpace(Description) && Description != Merchant
        ? $"{CategoryName} • {Description}"
        : CategoryName;

    [JsonIgnore]
    public string CategoryName { get; set; } = string.Empty;

    [JsonIgnore]
    public string SourceAccountName { get; set; } = string.Empty;

    [JsonIgnore]
    public string DestinationAccountName { get; set; } = string.Empty;

    [JsonIgnore]
    public string CategoryIcon { get; set; } = "category";

    [JsonIgnore]
    public long CategoryColorHex { get; set; } = 0xFF607D8B;

    [JsonIgnore]
    public string CategoryColorString => $"#{((uint)CategoryColorHex):X8}";

    [JsonIgnore]
    public string FormattedAmount
    {
        get
        {
            string sign = Type switch
            {
                TransactionType.INCOME or TransactionType.REFUND => "+",
                TransactionType.EXPENSE => "-",
                _ => ""
            };
            return $"{sign}{Amount.Formatted()}";
        }
    }

    [JsonIgnore]
    public string AmountColor => Type switch
    {
        TransactionType.INCOME or TransactionType.REFUND => "#2E7D32", // FinPulse Primary Green
        TransactionType.EXPENSE => "#D32F2F", // Red
        TransactionType.TRANSFER => "#1976D2", // Blue
        _ => "#757575"
    };
}
