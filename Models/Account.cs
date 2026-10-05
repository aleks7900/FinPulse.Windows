using System;
using System.Text.Json.Serialization;

namespace FinPulse.Windows.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AccountType
{
    CASH,
    BANK,
    CREDIT_CARD,
    SAVINGS,
    INVESTMENT,
    WALLET,
    LOAN,
    OTHER
}

public class Account
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public AccountType Type { get; set; } = AccountType.BANK;

    private Money _balance = Money.Zero("USD");
    [JsonPropertyName("balance")]
    public Money Balance
    {
        get => _balance;
        set
        {
            _balance = value;
            if (_availableBalance.IsZero || _availableBalance.CurrencyCode != value.CurrencyCode)
            {
                _availableBalance = new Money(_availableBalance.AmountMinor, value.CurrencyCode);
            }
        }
    }

    private Money _availableBalance = Money.Zero("USD");
    [JsonPropertyName("availableBalance")]
    public Money AvailableBalance
    {
        get => _availableBalance;
        set => _availableBalance = value;
    }

    [JsonPropertyName("creditLimit")]
    public Money? CreditLimit { get; set; }

    [JsonPropertyName("institution")]
    public string? Institution { get; set; }

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "account_balance";

    [JsonPropertyName("colorHex")]
    public long ColorHex { get; set; } = 0xFF2196F3;

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("isArchived")]
    public bool IsArchived { get; set; } = false;

    [JsonPropertyName("createdAt")]
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    [JsonPropertyName("updatedAt")]
    public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    [JsonIgnore]
    public string CurrencyCode => Balance.CurrencyCode;

    [JsonIgnore]
    public string FormattedBalance => Balance.Formatted();

    [JsonIgnore]
    public string TypeDisplayName => Type switch
    {
        AccountType.CASH => "Cash",
        AccountType.BANK => "Bank Account",
        AccountType.CREDIT_CARD => "Credit Card",
        AccountType.SAVINGS => "Savings Account",
        AccountType.INVESTMENT => "Investment Account",
        AccountType.WALLET => "Digital Wallet",
        AccountType.LOAN => "Loan",
        _ => "Other"
    };

    [JsonIgnore]
    public string GlyphIcon => Type switch
    {
        AccountType.CASH => "\uE8C7", // Banknote / Money
        AccountType.BANK => "\uE825", // Building
        AccountType.CREDIT_CARD => "\uE8C7", // PaymentCard
        AccountType.SAVINGS => "\uF580", // PiggyBank
        AccountType.INVESTMENT => "\uE9D9", // Financial / Stock
        AccountType.WALLET => "\uE8C7", // Wallet
        _ => "\uE825"
    };

    [JsonIgnore]
    public string HexColorString => $"#{((uint)ColorHex):X8}";
}
