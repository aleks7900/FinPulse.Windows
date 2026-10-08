using System;
using System.Collections.Generic;

namespace FinPulse.Windows.Services;

public record LanguageOption(string Code, string NativeName, string EnglishName);

public interface ILocalizationService
{
    string CurrentLanguage { get; }
    IReadOnlyList<LanguageOption> SupportedLanguages { get; }
    
    event EventHandler? LanguageChanged;

    string GetString(string resourceKey, string? fallback = null);
    string Format(string resourceKey, params object[] args);
    void ApplyLanguage(string languageCode);
    string GetEffectiveLanguageCode();
}
