using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FinPulse.Windows.Models;

namespace FinPulse.Windows.Services;

public class CloudSyncService : ISyncService
{
    private readonly ILocalDataStore _localStore;
    private readonly IFirestoreClient _firestoreClient;
    private readonly IAuthService _authService;

    public SyncStatus CurrentStatus { get; private set; } = SyncStatus.IDLE;
    public long LastSyncTimestamp { get; private set; } = 0L;
    public int PendingChangesCount => _pendingCount;
    public string? LastErrorMessage { get; private set; }

    public event EventHandler<SyncStatus>? SyncStatusChanged;

    private int _pendingCount = 0;

    public CloudSyncService(
        ILocalDataStore localStore,
        IFirestoreClient firestoreClient,
        IAuthService authService)
    {
        _localStore = localStore;
        _firestoreClient = firestoreClient;
        _authService = authService;

        _localStore.DataChanged += async (s, e) =>
        {
            var pending = await _localStore.GetPendingSyncItemsAsync();
            _pendingCount = pending.Count;
        };
    }

    public async Task<SyncResult> PerformFullSyncAsync()
    {
        var user = _authService.CurrentUser;
        if (user == null || string.IsNullOrWhiteSpace(user.Uid))
        {
            CurrentStatus = SyncStatus.IDLE;
            return new SyncResult { IsSuccess = false, ErrorMessage = "No user logged in" };
        }

        SetStatus(SyncStatus.SYNCING);
        try
        {
            // 1. Upload pending local changes
            var uploadRes = await UploadPendingChangesInternalAsync(user.Uid);

            // 2. Download remote changes since LastSyncTimestamp
            var downloadRes = await DownloadRemoteChangesInternalAsync(user.Uid, LastSyncTimestamp);

            // 3. Sync Settings
            await SyncSettingsInternalAsync(user.Uid);

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            LastSyncTimestamp = now;
            LastErrorMessage = null;
            SetStatus(SyncStatus.SUCCESS);

            return new SyncResult
            {
                IsSuccess = true,
                UploadedCount = uploadRes.UploadedCount,
                DownloadedCount = downloadRes.DownloadedCount,
                DeletedCount = uploadRes.DeletedCount + downloadRes.DeletedCount,
                ConflictsResolvedCount = downloadRes.ConflictsResolvedCount,
                SyncedAt = now
            };
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            SetStatus(SyncStatus.ERROR);
            return new SyncResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<SyncResult> UploadPendingChangesAsync()
    {
        var user = _authService.CurrentUser;
        if (user == null || string.IsNullOrWhiteSpace(user.Uid))
            return new SyncResult { IsSuccess = false, ErrorMessage = "No user logged in" };

        SetStatus(SyncStatus.SYNCING);
        try
        {
            var res = await UploadPendingChangesInternalAsync(user.Uid);
            SetStatus(SyncStatus.SUCCESS);
            return res;
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            SetStatus(SyncStatus.ERROR);
            return new SyncResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<SyncResult> DownloadRemoteChangesAsync()
    {
        var user = _authService.CurrentUser;
        if (user == null || string.IsNullOrWhiteSpace(user.Uid))
            return new SyncResult { IsSuccess = false, ErrorMessage = "No user logged in" };

        SetStatus(SyncStatus.SYNCING);
        try
        {
            var res = await DownloadRemoteChangesInternalAsync(user.Uid, LastSyncTimestamp);
            SetStatus(SyncStatus.SUCCESS);
            return res;
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            SetStatus(SyncStatus.ERROR);
            return new SyncResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }

    private async Task<SyncResult> UploadPendingChangesInternalAsync(string uid)
    {
        var pending = await _localStore.GetPendingSyncItemsAsync();
        int uploaded = 0;
        int deleted = 0;

        // Group by collection/type
        var byType = pending.GroupBy(p => p.EntityType);

        foreach (var group in byType)
        {
            string entityType = group.Key;
            string collection = GetCollectionName(entityType);

            var toUploadRecords = new List<CloudEntityRecord>();

            foreach (var item in group)
            {
                if (item.Action == "DELETE")
                {
                    await _firestoreClient.RecordTombstoneAsync(uid, collection, item.EntityId, item.Timestamp);
                    await _localStore.RemoveFromSyncQueueAsync(item.EntityType, item.EntityId);
                    deleted++;
                }
                else
                {
                    string? payload = await SerializeEntityAsync(entityType, item.EntityId);
                    if (!string.IsNullOrEmpty(payload))
                    {
                        toUploadRecords.Add(new CloudEntityRecord
                        {
                            Id = item.EntityId,
                            Collection = collection,
                            JsonPayload = payload,
                            UpdatedAt = item.Timestamp,
                            IsDeleted = false
                        });
                    }
                }
            }

            if (toUploadRecords.Count > 0)
            {
                int count = await _firestoreClient.UploadRecordsAsync(uid, collection, toUploadRecords);
                uploaded += count;
                foreach (var rec in toUploadRecords)
                {
                    await _localStore.RemoveFromSyncQueueAsync(entityType, rec.Id);
                }
            }
        }

        var remaining = await _localStore.GetPendingSyncItemsAsync();
        _pendingCount = remaining.Count;

        return new SyncResult { IsSuccess = true, UploadedCount = uploaded, DeletedCount = deleted };
    }

    private async Task<SyncResult> DownloadRemoteChangesInternalAsync(string uid, long sinceTimestamp)
    {
        int downloaded = 0;
        int deleted = 0;

        string[] collections = {
            "accounts", "categories", "transactions", "budgets",
            "recurring_rules", "goals"
        };

        foreach (var collection in collections)
        {
            var records = await _firestoreClient.DownloadRecordsAsync(uid, collection, sinceTimestamp);
            foreach (var rec in records)
            {
                if (rec.IsDeleted)
                {
                    await DeleteEntityLocallyAsync(collection, rec.Id);
                    deleted++;
                }
                else if (!string.IsNullOrWhiteSpace(rec.JsonPayload))
                {
                    await UpsertEntityLocallyAsync(collection, rec.JsonPayload);
                    downloaded++;
                }
            }
        }

        return new SyncResult { IsSuccess = true, DownloadedCount = downloaded, DeletedCount = deleted };
    }

    private async Task SyncSettingsInternalAsync(string uid)
    {
        var localSettings = await _localStore.GetSettingsAsync();
        var remoteRecords = await _firestoreClient.DownloadRecordsAsync(uid, CloudSettings.SettingsCollection, 0);
        var appSettingDoc = remoteRecords.FirstOrDefault(r => r.Id == CloudSettings.SettingsDocumentId && !r.IsDeleted);

        if (appSettingDoc != null && !string.IsNullOrWhiteSpace(appSettingDoc.JsonPayload))
        {
            var remoteSettings = JsonSerializer.Deserialize<CloudSettings>(appSettingDoc.JsonPayload);
            if (remoteSettings != null)
            {
                if (remoteSettings.UpdatedAt > localSettings.UpdatedAt)
                {
                    // Remote is newer, apply to local
                    await _localStore.SaveSettingsAsync(remoteSettings, markForSync: false);
                    return;
                }
            }
        }

        // Otherwise push local settings to cloud
        string payload = JsonSerializer.Serialize(localSettings);
        var record = new CloudEntityRecord
        {
            Id = CloudSettings.SettingsDocumentId,
            Collection = CloudSettings.SettingsCollection,
            JsonPayload = payload,
            UpdatedAt = localSettings.UpdatedAt
        };
        await _firestoreClient.UploadRecordsAsync(uid, CloudSettings.SettingsCollection, new List<CloudEntityRecord> { record });
    }

    public async Task ClearCloudDataAsync()
    {
        var user = _authService.CurrentUser;
        if (user == null || string.IsNullOrWhiteSpace(user.Uid)) return;

        SetStatus(SyncStatus.SYNCING);
        try
        {
            await _firestoreClient.ClearUserStorageAsync(user.Uid);
            LastSyncTimestamp = 0;
            SetStatus(SyncStatus.IDLE);
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            SetStatus(SyncStatus.ERROR);
        }
    }

    private async Task<string?> SerializeEntityAsync(string entityType, string id)
    {
        return entityType switch
        {
            "ACCOUNT" => (await _localStore.GetAccountByIdAsync(id)) is { } a ? JsonSerializer.Serialize(a) : null,
            "TRANSACTION" => (await _localStore.GetTransactionByIdAsync(id)) is { } t ? JsonSerializer.Serialize(t) : null,
            "CATEGORY" => (await _localStore.GetCategoryByIdAsync(id)) is { } c ? JsonSerializer.Serialize(c) : null,
            "BUDGET" => (await _localStore.GetBudgetByIdAsync(id)) is { } b ? JsonSerializer.Serialize(b) : null,
            "RECURRING_RULE" => (await _localStore.GetRecurringRuleByIdAsync(id)) is { } r ? JsonSerializer.Serialize(r) : null,
            "GOAL" => (await _localStore.GetGoalByIdAsync(id)) is { } g ? JsonSerializer.Serialize(g) : null,
            "SETTINGS" => JsonSerializer.Serialize(await _localStore.GetSettingsAsync()),
            _ => null
        };
    }

    private async Task UpsertEntityLocallyAsync(string collection, string jsonPayload)
    {
        try
        {
            switch (collection)
            {
                case "accounts":
                    if (JsonSerializer.Deserialize<Account>(jsonPayload) is { } acc)
                        await _localStore.UpsertAccountAsync(acc, markForSync: false);
                    break;
                case "transactions":
                    if (JsonSerializer.Deserialize<Transaction>(jsonPayload) is { } tx)
                        await _localStore.UpsertTransactionAsync(tx, markForSync: false);
                    break;
                case "categories":
                    if (JsonSerializer.Deserialize<Category>(jsonPayload) is { } cat)
                        await _localStore.UpsertCategoryAsync(cat, markForSync: false);
                    break;
                case "budgets":
                    if (JsonSerializer.Deserialize<Budget>(jsonPayload) is { } b)
                        await _localStore.UpsertBudgetAsync(b, markForSync: false);
                    break;
                case "recurring_rules":
                    if (JsonSerializer.Deserialize<RecurringTransaction>(jsonPayload) is { } r)
                        await _localStore.UpsertRecurringRuleAsync(r, markForSync: false);
                    break;
                case "goals":
                    if (JsonSerializer.Deserialize<FinancialGoal>(jsonPayload) is { } g)
                        await _localStore.UpsertGoalAsync(g, markForSync: false);
                    break;
            }
        }
        catch { }
    }

    private async Task DeleteEntityLocallyAsync(string collection, string id)
    {
        switch (collection)
        {
            case "accounts":
                await _localStore.DeleteAccountAsync(id, markForSync: false);
                break;
            case "transactions":
                await _localStore.DeleteTransactionAsync(id, markForSync: false);
                break;
            case "categories":
                await _localStore.DeleteCategoryAsync(id, markForSync: false);
                break;
            case "budgets":
                await _localStore.DeleteBudgetAsync(id, markForSync: false);
                break;
            case "recurring_rules":
                await _localStore.DeleteRecurringRuleAsync(id, markForSync: false);
                break;
            case "goals":
                await _localStore.DeleteGoalAsync(id, markForSync: false);
                break;
        }
    }

    private string GetCollectionName(string entityType)
    {
        return entityType switch
        {
            "ACCOUNT" => "accounts",
            "CATEGORY" => "categories",
            "TRANSACTION" => "transactions",
            "BUDGET" => "budgets",
            "RECURRING_RULE" => "recurring_rules",
            "GOAL" => "goals",
            "SETTINGS" => "settings",
            _ => entityType.ToLowerInvariant()
        };
    }

    private void SetStatus(SyncStatus status)
    {
        CurrentStatus = status;
        SyncStatusChanged?.Invoke(this, status);
    }
}
