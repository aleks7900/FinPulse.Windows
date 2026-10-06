using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FinPulse.Windows.Models;

public class SyncPreferences
{
    [JsonPropertyName("isAutoSyncEnabled")]
    public bool IsAutoSyncEnabled { get; set; } = true;

    [JsonPropertyName("isSyncOnStartupEnabled")]
    public bool IsSyncOnStartupEnabled { get; set; } = true;

    [JsonPropertyName("syncIntervalMinutes")]
    public long SyncIntervalMinutes { get; set; } = 60L;
}

public class SyncIntervalOption
{
    public long Minutes { get; set; }
    public string DisplayName { get; set; } = string.Empty;

    public static readonly List<SyncIntervalOption> DefaultOptions = new()
    {
        new SyncIntervalOption { Minutes = 15, DisplayName = "15 minutes" },
        new SyncIntervalOption { Minutes = 30, DisplayName = "30 minutes" },
        new SyncIntervalOption { Minutes = 60, DisplayName = "1 hour" },
        new SyncIntervalOption { Minutes = 180, DisplayName = "3 hours" },
        new SyncIntervalOption { Minutes = 360, DisplayName = "6 hours" },
        new SyncIntervalOption { Minutes = 720, DisplayName = "12 hours" },
        new SyncIntervalOption { Minutes = 1440, DisplayName = "24 hours" }
    };
}
