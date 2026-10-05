using System;
using System.Globalization;
using System.Text.Json.Serialization;

namespace FinPulse.Windows.Models;

/// <summary>
/// Safe monetary value representation mirroring FinPulse Android Money model.
/// Stored internally as minor units (e.g. cents, 1000 = $10.00) using 64-bit Long.
/// Prohibits floating-point precision loss.
/// </summary>
public readonly struct Money : IComparable<Money>, IEquatable<Money>
{
    [JsonPropertyName("amountMinor")]
    public long AmountMinor { get; init; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; }

    [JsonConstructor]
    public Money(long amountMinor, string currencyCode = "USD")
    {
        AmountMinor = amountMinor;
        CurrencyCode = string.IsNullOrWhiteSpace(currencyCode) ? "USD" : currencyCode.ToUpperInvariant();
    }

    [JsonIgnore]
    public decimal AmountDecimal => CurrencyConfig.ToMajor(AmountMinor, CurrencyCode);

    [JsonIgnore]
    public double AmountDouble => (double)AmountDecimal;

    [JsonIgnore]
    public bool IsZero => AmountMinor == 0;

    [JsonIgnore]
    public bool IsPositive => AmountMinor > 0;

    [JsonIgnore]
    public bool IsNegative => AmountMinor < 0;

    public static Money Zero(string currencyCode = "USD") => new(0, currencyCode);

    public static Money FromMajor(decimal amount, string currencyCode = "USD")
    {
        long minor = CurrencyConfig.ToMinor(amount, currencyCode);
        return new Money(minor, currencyCode);
    }

    public static Money FromDouble(double amount, string currencyCode = "USD")
    {
        return FromMajor((decimal)amount, currencyCode);
    }

    public static Money FromCents(long cents, string currencyCode = "USD")
    {
        return new Money(cents, currencyCode);
    }

    public Money Absolute() => new(Math.Abs(AmountMinor), CurrencyCode);

    public string Formatted() => CurrencyConfig.Format(this, null);

    public string Formatted(CultureInfo? culture) => CurrencyConfig.Format(this, culture);

    [JsonIgnore]
    public string FormattedString => Formatted();

    public string FormattedCompact()
    {
        decimal absMajor = Math.Abs(AmountDecimal);
        string sign = IsNegative ? "-" : "";
        string symbol = CurrencyConfig.GetSymbol(CurrencyCode);

        if (absMajor >= 1_000_000m)
            return string.Format(CultureInfo.InvariantCulture, "{0}{1}{2:F1}M", sign, symbol, absMajor / 1_000_000m);
        if (absMajor >= 1_000m)
            return string.Format(CultureInfo.InvariantCulture, "{0}{1}{2:F1}K", sign, symbol, absMajor / 1_000m);

        return Formatted();
    }

    public static Money operator +(Money a, Money b)
    {
        if (a.CurrencyCode != b.CurrencyCode)
            throw new InvalidOperationException($"Currency mismatch: {a.CurrencyCode} vs {b.CurrencyCode}");
        return new Money(a.AmountMinor + b.AmountMinor, a.CurrencyCode);
    }

    public static Money operator -(Money a, Money b)
    {
        if (a.CurrencyCode != b.CurrencyCode)
            throw new InvalidOperationException($"Currency mismatch: {a.CurrencyCode} vs {b.CurrencyCode}");
        return new Money(a.AmountMinor - b.AmountMinor, a.CurrencyCode);
    }

    public static Money operator -(Money a) => new(-a.AmountMinor, a.CurrencyCode);

    public static Money operator *(Money a, double factor)
    {
        decimal scaled = Math.Round((decimal)a.AmountMinor * (decimal)factor, 0, MidpointRounding.ToEven);
        return new Money((long)scaled, a.CurrencyCode);
    }

    public static Money operator /(Money a, double divisor)
    {
        if (Math.Abs(divisor) < 0.000000001)
            throw new DivideByZeroException("Cannot divide Money by zero");
        decimal scaled = Math.Round((decimal)a.AmountMinor / (decimal)divisor, 0, MidpointRounding.ToEven);
        return new Money((long)scaled, a.CurrencyCode);
    }

    public static bool operator >(Money a, Money b) => a.CompareTo(b) > 0;
    public static bool operator <(Money a, Money b) => a.CompareTo(b) < 0;
    public static bool operator >=(Money a, Money b) => a.CompareTo(b) >= 0;
    public static bool operator <=(Money a, Money b) => a.CompareTo(b) <= 0;
    public static bool operator ==(Money a, Money b) => a.Equals(b);
    public static bool operator !=(Money a, Money b) => !a.Equals(b);

    public int CompareTo(Money other)
    {
        if (CurrencyCode != other.CurrencyCode)
            throw new InvalidOperationException($"Cannot compare different currencies: {CurrencyCode} and {other.CurrencyCode}");
        return AmountMinor.CompareTo(other.AmountMinor);
    }

    public bool Equals(Money other) => AmountMinor == other.AmountMinor && CurrencyCode == other.CurrencyCode;

    public override bool Equals(object? obj) => obj is Money other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(AmountMinor, CurrencyCode);

    public override string ToString() => Formatted();
}
