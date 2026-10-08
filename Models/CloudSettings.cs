using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FinPulse.Windows.Models;

public class CloudSettings
{
    public const int CurrentSchemaVersion = 1;
    public const string SettingsCollection = "settings";
    public const string SettingsDocumentId = "app";

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    [JsonPropertyName("baseCurrencyCode")]
    public string BaseCurrencyCode { get; set; } = "USD";

    [JsonPropertyName("selectedLanguage")]
    public string SelectedLanguage { get; set; } = "SYSTEM";

    [JsonPropertyName("darkMode")]
    public string? DarkMode { get; set; } = "SYSTEM";

    [JsonPropertyName("hideBalances")]
    public bool HideBalances { get; set; } = false;

    [JsonPropertyName("widgetPrivacyEnabled")]
    public bool WidgetPrivacyEnabled { get; set; } = false;

    [JsonPropertyName("widgetMaskOnAppLock")]
    public bool WidgetMaskOnAppLock { get; set; } = true;

    [JsonPropertyName("lockTimeout")]
    public string LockTimeout { get; set; } = "MINUTE_1";

    [JsonPropertyName("backupReminderInterval")]
    public string BackupReminderInterval { get; set; } = "OFF";

    [JsonPropertyName("digestFrequency")]
    public string DigestFrequency { get; set; } = "DAILY";

    [JsonPropertyName("digestPrivacyEnabled")]
    public bool DigestPrivacyEnabled { get; set; } = false;

    [JsonPropertyName("digestDeliveryHour")]
    public int DigestDeliveryHour { get; set; } = 20;

    [JsonPropertyName("dismissedInboxItemIds")]
    public List<string> DismissedInboxItemIds { get; set; } = new();

    [JsonPropertyName("updatedAt")]
    public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    [JsonPropertyName("deviceModel")]
    public string? DeviceModel { get; set; } = "Windows 11 Companion";

    [JsonPropertyName("hasSeededInitialData")]
    public bool HasSeededInitialData { get; set; } = false;
}
