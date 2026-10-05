using System;
using System.Threading.Tasks;
using FinPulse.Windows.Models;

namespace FinPulse.Windows.Services;

public enum SyncStatus
{
    IDLE,
    SYNCING,
    SUCCESS,
    ERROR
}

public interface ISyncService
{
    SyncStatus CurrentStatus { get; }
    long LastSyncTimestamp { get; }
    int PendingChangesCount { get; }
    string? LastErrorMessage { get; }

    event EventHandler<SyncStatus>? SyncStatusChanged;

    Task<SyncResult> PerformFullSyncAsync();
    Task<SyncResult> UploadPendingChangesAsync();
    Task<SyncResult> DownloadRemoteChangesAsync();
    Task ClearCloudDataAsync();
}
