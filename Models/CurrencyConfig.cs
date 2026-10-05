using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FinPulse.Windows.Models;

public record CurrencyMetadata(
    string Code,
    string DisplayName,
    string Symbol,
    int FractionDigits,
    string FlagEmoji
)
{
    public long MinorMultiplier => FractionDigits switch
    {
        0 => 1L,
        1 => 10L,
        2 => 100L,
        3 => 1000L,
        4 => 10000L,
        _ => 100L
    };
}

public static class CurrencyConfig
{
    public static readonly List<CurrencyMetadata> SupportedCurrencies = new()
    {
        new("USD", "US Dollar", "$", 2, "🇺🇸"),
        new("EUR", "Euro", "€", 2, "🇪🇺"),
        new("GBP", "British Pound", "£", 2, "🇬🇧"),
        new("MDL", "Moldovan Leu", "L", 2, "🇲🇩"),
        new("RON", "Romanian Leu", "lei", 2, "🇷🇴"),
        new("UAH", "Ukrainian Hryvnia", "₴", 2, "🇺🇦"),
        new("PLN", "Polish Zloty", "zł", 2, "🇵🇱"),
        new("CHF", "Swiss Franc", "CHF", 2, "🇨🇭"),
        new("JPY", "Japanese Yen", "¥", 0, "🇯🇵"),
        new("CNY", "Chinese Yuan", "¥", 2, "🇨🇳"),
        new("CAD", "Canadian Dollar", "CA$", 2, "🇨🇦"),
        new("AUD", "Australian Dollar", "A$", 2, "🇦🇺"),
        new("BRL", "Brazilian Real", "R$", 2, "🇧🇷"),
        new("TRY", "Turkish Lira", "₺", 2, "🇹🇷"),
        new("KRW", "South Korean Won", "₩", 0, "🇰🇷"),
        new("INR", "Indian Rupee", "₹", 2, "🇮🇳"),
        new("MXN", "Mexican Peso", "Mex$", 2, "🇲🇽"),
        new("SEK", "Swedish Krona", "kr", 2, "🇸🇪"),
        new("NOK", "Norwegian Krone", "kr", 2, "🇳🇴"),
        new("DKK", "Danish Krone", "kr", 2, "🇩🇰"),
        new("CZK", "Czech Koruna", "Kč", 2, "🇨🇿"),
        new("HUF", "Hungarian Forint", "Ft", 0, "🇭🇺"),
        new("BGN", "Bulgarian Lev", "лв", 2, "🇧🇬"),
        new("SGD", "Singapore Dollar", "S$", 2, "🇸🇬"),
        new("HKD", "Hong Kong Dollar", "HK$", 2, "🇭🇰"),
        new("NZD", "New Zealand Dollar", "NZ$", 2, "🇳🇿"),
        new("AED", "UAE Dirham", "AED", 2, "🇦🇪"),
        new("SAR", "Saudi Riyal", "SAR", 2, "🇸🇦"),
        new("ILS", "Israeli New Shekel", "₪", 2, "🇮🇱"),
        new("ZAR", "South African Rand", "R", 2, "🇿🇦"),
        new("BHD", "Bahraini Dinar", "BD", 3, "🇧🇭"),
        new("KWD", "Kuwaiti Dinar", "KD", 3, "🇰🇼"),
        new("OMR", "Omani Rial", "OMR", 3, "🇴🇲")
    };

    private static readonly Dictionary<string, CurrencyMetadata> CurrencyMap =
        SupportedCurrencies.ToDictionary(c => c.Code.ToUpperInvariant(), c => c);

    public static CurrencyMetadata GetMetadata(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
            currencyCode = "USD";

        string code = currencyCode.ToUpperInvariant();
        if (CurrencyMap.TryGetValue(code, out var meta))
            return meta;

        return new CurrencyMetadata(code, code, code, 2, "🌐");
    }

    public static string GetSymbol(string currencyCode) => GetMetadata(currencyCode).Symbol;

    public static string GetName(string currencyCode) => GetMetadata(currencyCode).DisplayName;

    public static int DecimalsFor(string currencyCode) => GetMetadata(currencyCode).FractionDigits;

    public static long MinorUnitsPerMajor(string currencyCode) => GetMetadata(currencyCode).MinorMultiplier;

    public static long ToMinor(decimal amount, string currencyCode)
    {
        long multiplier = MinorUnitsPerMajor(currencyCode);
        return (long)Math.Round(amount * multiplier, 0, MidpointRounding.ToEven);
    }

    public static decimal ToMajor(long amountMinor, string currencyCode)
    {
        long divisor = MinorUnitsPerMajor(currencyCode);
        return Math.Round((decimal)amountMinor / divisor, DecimalsFor(currencyCode), MidpointRounding.ToEven);
    }

    public static string Format(Money money, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var meta = GetMetadata(money.CurrencyCode);
        decimal major = ToMajor(money.AmountMinor, money.CurrencyCode);

        string format = meta.FractionDigits switch
        {
            0 => "N0",
            1 => "N1",
            3 => "N3",
            _ => "N2"
        };

        string formattedNumber = major.ToString(format, culture);
        // Prefix symbol
        if (major < 0)
        {
            decimal absMajor = Math.Abs(major);
            return $"-{meta.Symbol}{absMajor.ToString(format, culture)}";
        }
        return $"{meta.Symbol}{formattedNumber}";
    }
}
