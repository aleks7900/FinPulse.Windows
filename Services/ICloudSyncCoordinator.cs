using System;
using System.Threading;
using System.Threading.Tasks;
using FinPulse.Windows.Models;

namespace FinPulse.Windows.Services;

public enum GlobalSyncState
{
    Idle,
    Syncing,
    Success,
    Error,
    Offline,
    AuthenticationRequired
}

public enum SyncTrigger
{
    Manual,
    Startup,
    Scheduled,
    Foreground,
    ConnectivityRestored,
    LocalChange
}

public interface ICloudSyncCoordinator : IDisposable
{
    GlobalSyncState State { get; }
    bool IsSyncing { get; }
    DateTimeOffset? LastSyncTime { get; }
    long LastSyncTimestamp { get; }
    int PendingChangesCount { get; }
    string? LastError { get; }

    bool IsAutoSyncEnabled { get; }
    bool IsSyncOnStartupEnabled { get; }
    long SyncIntervalMinutes { get; }

    event EventHandler<GlobalSyncState>? StateChanged;

    Task InitializeAsync();
    Task<SyncResult> SyncAsync(SyncTrigger trigger = SyncTrigger.Manual, CancellationToken cancellationToken = default);
    void TriggerStartupSync();
    void TriggerForegroundSync();
    Task SetAutoSyncEnabledAsync(bool enabled);
    Task SetSyncOnStartupEnabledAsync(bool enabled);
    Task SetSyncIntervalMinutesAsync(long minutes);
}
