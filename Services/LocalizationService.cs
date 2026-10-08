using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Globalization;

namespace FinPulse.Windows.Services;

public class LocalizationService : ILocalizationService
{
    private static LocalizationService? _currentInstance;
    public static LocalizationService Current => _currentInstance ??= new LocalizationService();

    private ResourceLoader? _resourceLoader;
    private string _currentLanguage = "SYSTEM";
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _reswCache = new(StringComparer.OrdinalIgnoreCase);

    public string CurrentLanguage => _currentLanguage;

    public event EventHandler? LanguageChanged;

    private static readonly string[] KnownLanguageCodes =
    [
        "en-US", "de-DE", "fr-FR", "es-ES", "it-IT", "pt-PT",
        "pl-PL", "ro-RO", "uk-UA", "zh-CN", "ja-JP", "ko-KR", "ru-RU"
    ];

    public IReadOnlyList<LanguageOption> SupportedLanguages
    {
        get
        {
            string systemDefaultName = "System default";
            try
            {
                string activeLang = GetEffectiveLanguageCode();
                string? res = ResolveFromResw(activeLang, "Language_SystemDefault");
                if (!string.IsNullOrEmpty(res))
                    systemDefaultName = res;
            }
            catch { }

            return new List<LanguageOption>
            {
                new("SYSTEM", systemDefaultName, "System default"),
                new("en-US", "English", "English"),
                new("de-DE", "Deutsch", "German"),
                new("fr-FR", "Français", "French"),
                new("es-ES", "Español", "Spanish"),
                new("it-IT", "Italiano", "Italian"),
                new("pt-PT", "Português", "Portuguese"),
                new("pl-PL", "Polski", "Polish"),
                new("ro-RO", "Română", "Romanian"),
                new("uk-UA", "Українська", "Ukrainian"),
                new("ru-RU", "Русский", "Russian"),
                new("zh-CN", "简体中文", "Chinese (Simplified)"),
                new("ja-JP", "日本語", "Japanese"),
                new("ko-KR", "한국어", "Korean")
            };
        }
    }

    public LocalizationService()
    {
        _currentInstance ??= this;
        InitializeLoader();
    }

    private void InitializeLoader()
    {
        try
        {
            _resourceLoader = new ResourceLoader();
        }
        catch
        {
            _resourceLoader = null;
        }
    }

    public void ApplyLanguage(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode) || languageCode.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase))
        {
            _currentLanguage = "SYSTEM";
            try
            {
                ApplicationLanguages.PrimaryLanguageOverride = string.Empty;
            }
            catch { }

            CultureInfo systemCulture;
            try
            {
                var prefLang = global::Windows.System.UserProfile.GlobalizationPreferences.Languages?.FirstOrDefault()
                               ?? ApplicationLanguages.Languages?.FirstOrDefault();
                systemCulture = !string.IsNullOrWhiteSpace(prefLang)
                    ? new CultureInfo(prefLang)
                    : CultureInfo.InstalledUICulture;
            }
            catch
            {
                systemCulture = CultureInfo.InstalledUICulture;
            }

            try
            {
                CultureInfo.DefaultThreadCurrentCulture = systemCulture;
                CultureInfo.DefaultThreadCurrentUICulture = systemCulture;
                Thread.CurrentThread.CurrentCulture = systemCulture;
                Thread.CurrentThread.CurrentUICulture = systemCulture;
            }
            catch { }
        }
        else
        {
            _currentLanguage = languageCode;
            try
            {
                ApplicationLanguages.PrimaryLanguageOverride = languageCode;
            }
            catch { }

            try
            {
                var culture = new CultureInfo(languageCode);
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;
            }
            catch { }
        }

        InitializeLoader();
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetString(string resourceKey, string? fallback = null)
    {
        if (string.IsNullOrEmpty(resourceKey))
            return string.Empty;

        string activeLang = GetEffectiveLanguageCode();

        // 1. Resolve from loaded dictionary for active language (ensures immediate accuracy in all runtimes)
        string? reswVal = ResolveFromResw(activeLang, resourceKey);
        if (!string.IsNullOrEmpty(reswVal))
            return reswVal;

        // 2. Try MRT Core ResourceLoader
        if (_resourceLoader != null)
        {
            try
            {
                string val = _resourceLoader.GetString(resourceKey);
                if (!string.IsNullOrEmpty(val))
                    return val;

                // Try suffix lookups (.Text, .Content, .Header, .Title, .Message)
                foreach (var suffix in new[] { ".Text", ".Content", ".Header", ".Title", ".Message", ".PlaceholderText" })
                {
                    val = _resourceLoader.GetString(resourceKey + suffix);
                    if (!string.IsNullOrEmpty(val))
                        return val;
                }
            }
            catch { }
        }

        // 3. Fallback to base language (en-US)
        if (!activeLang.Equals("en-US", StringComparison.OrdinalIgnoreCase))
        {
            reswVal = ResolveFromResw("en-US", resourceKey);
            if (!string.IsNullOrEmpty(reswVal))
                return reswVal;
        }

        return fallback ?? resourceKey;
    }

    public string Format(string resourceKey, params object[] args)
    {
        string pattern = GetString(resourceKey);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, pattern, args);
        }
        catch
        {
            return pattern;
        }
    }

    public string GetEffectiveLanguageCode()
    {
        if (_currentLanguage != "SYSTEM" && !string.IsNullOrWhiteSpace(_currentLanguage))
            return _currentLanguage;

        string? prefLang = null;
        try
        {
            prefLang = global::Windows.System.UserProfile.GlobalizationPreferences.Languages?.FirstOrDefault()
                       ?? ApplicationLanguages.Languages?.FirstOrDefault();
        }
        catch { }

        string sysLang = !string.IsNullOrWhiteSpace(prefLang) ? prefLang : CultureInfo.CurrentUICulture.Name;
        // Match exact or prefix (e.g. de -> de-DE, uk -> uk-UA)
        string? match = KnownLanguageCodes.FirstOrDefault(code => code.Equals(sysLang, StringComparison.OrdinalIgnoreCase))
                     ?? KnownLanguageCodes.FirstOrDefault(code => sysLang.StartsWith(code[..2], StringComparison.OrdinalIgnoreCase));

        return match ?? "en-US";
    }

    private string? ResolveFromResw(string languageCode, string key)
    {
        var dict = GetOrLoadReswDictionary(languageCode);
        if (dict == null)
            return null;

        if (dict.TryGetValue(key, out var val))
            return val;

        // Try suffixes
        foreach (var suffix in new[] { ".Text", ".Content", ".Header", ".Title", ".Message", ".PlaceholderText" })
        {
            if (dict.TryGetValue(key + suffix, out val))
                return val;
        }

        return null;
    }

    private static string? _customStringsDirectory;
    public static void SetStringsDirectory(string dir) => _customStringsDirectory = dir;

    public Dictionary<string, string> GetOrLoadReswDictionary(string languageCode)
    {
        return _reswCache.GetOrAdd(languageCode, lang =>
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string? reswPath = null;
            if (!string.IsNullOrEmpty(_customStringsDirectory))
            {
                string customPath = Path.Combine(_customStringsDirectory, lang, "Resources.resw");
                if (File.Exists(customPath)) reswPath = customPath;
            }

            if (reswPath == null)
            {
                string[] probeDirs =
                {
                    AppContext.BaseDirectory,
                    Path.Combine(AppContext.BaseDirectory, "..", "..", ".."),
                    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "FinPulse.Windows"),
                    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "FinPulse.Windows"),
                    Directory.GetCurrentDirectory(),
                    Path.Combine(Directory.GetCurrentDirectory(), "FinPulse.Windows")
                };

                foreach (var dir in probeDirs)
                {
                    string path = Path.GetFullPath(Path.Combine(dir, "Strings", lang, "Resources.resw"));
                    if (File.Exists(path))
                    {
                        reswPath = path;
                        break;
                    }
                }
            }

            if (reswPath == null || !File.Exists(reswPath))
                return dict;

            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    using var stream = new FileStream(reswPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    var doc = XDocument.Load(stream);
                    foreach (var data in doc.Descendants("data"))
                    {
                        var nameAttr = data.Attribute("name")?.Value;
                        var valElem = data.Element("value")?.Value;
                        if (!string.IsNullOrEmpty(nameAttr) && valElem != null)
                        {
                            dict[nameAttr] = valElem;
                        }
                    }
                    if (dict.Count > 0)
                        break;
                }
                catch (IOException)
                {
                    System.Threading.Thread.Sleep(50);
                }
                catch
                {
                    break;
                }
            }

            return dict;
        });
    }

    public void ClearCache()
    {
        _reswCache.Clear();
    }
}
