using System;
using System.Threading.Tasks;
using FinPulse.Windows.Models;

namespace FinPulse.Windows.Services;

public enum SyncStatus
{
    SIGNED_OUT,
    IDLE,
    SYNCING,
    SUCCESS,
    ERROR,
    OFFLINE,
    PENDING_CHANGES
}

public interface ISyncService
{
    SyncStatus CurrentStatus { get; }
    long LastSyncTimestamp { get; }
    int PendingChangesCount { get; }
    string? LastErrorMessage { get; }

    event EventHandler<SyncStatus>? SyncStatusChanged;

    Task<SyncResult> PerformFullSyncAsync(CancellationToken cancellationToken = default);
    Task<SyncResult> UploadPendingChangesAsync(CancellationToken cancellationToken = default);
    Task<SyncResult> DownloadRemoteChangesAsync(CancellationToken cancellationToken = default);
    Task ClearCloudDataAsync();
}
