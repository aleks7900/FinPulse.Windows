using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FinPulse.Windows.Models;

namespace FinPulse.Windows.Services;

public class CloudSyncService : ISyncService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILocalDataStore _localStore;
    private readonly IFirestoreClient _firestoreClient;
    private readonly IAuthService _authService;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    private readonly Timer _debounceTimer;
    private bool _isDisposed;

    public SyncStatus CurrentStatus { get; private set; } = SyncStatus.SIGNED_OUT;
    public long LastSyncTimestamp { get; private set; } = 0L;
    public int PendingChangesCount => _pendingCount;
    public string? LastErrorMessage { get; private set; }

    public event EventHandler<SyncStatus>? SyncStatusChanged;

    private int _pendingCount = 0;

    // Dependency-ordered entity types for PUSH
    private static readonly string[] PushOrder =
    [
        "ACCOUNT",
        "CATEGORY",
        "BUDGET",
        "GOAL",
        "RECURRING_RULE",
        "TRANSACTION",
        "SETTINGS"
    ];

    // Dependency-ordered collection names for PULL
    private static readonly string[] PullOrder =
    [
        "accounts",
        "categories",
        "budgets",
        "goals",
        "recurring_rules",
        "transactions"
    ];

    public CloudSyncService(
        ILocalDataStore localStore,
        IFirestoreClient firestoreClient,
        IAuthService authService)
    {
        _localStore = localStore;
        _firestoreClient = firestoreClient;
        _authService = authService;

        // Debounce timer for coalescing rapid local modifications (2.5 seconds)
        _debounceTimer = new Timer(OnDebounceTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);

        // Listen for local changes to track pending count and trigger debounced sync
        _localStore.DataChanged += OnLocalDataChanged;

        // Listen for auth changes to trigger sync on sign-in
        _authService.AuthStateChanged += OnAuthStateChanged;

        // Listen for network connectivity restored
        try
        {
            NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
        }
        catch { }

        CurrentStatus = _authService.IsLoggedIn ? SyncStatus.IDLE : SyncStatus.SIGNED_OUT;
    }

    private void OnLocalDataChanged(object? sender, EventArgs e)
    {
        _ = UpdatePendingCountAsync();

        // If online and logged in, debounce a sync pass
        if (_authService.IsLoggedIn && IsNetworkAvailable())
        {
            _debounceTimer.Change(2500, Timeout.Infinite);
        }
        else if (!IsNetworkAvailable() && _pendingCount > 0)
        {
            SetStatus(SyncStatus.PENDING_CHANGES);
        }
    }

    private async Task UpdatePendingCountAsync()
    {
        try
        {
            var pending = await _localStore.GetPendingSyncItemsAsync();
            _pendingCount = pending.Count;
            if (_pendingCount > 0 && CurrentStatus == SyncStatus.IDLE)
            {
                SetStatus(SyncStatus.PENDING_CHANGES);
            }
        }
        catch { }
    }

    private void OnAuthStateChanged(object? sender, UserSession? user)
    {
        if (user != null && !string.IsNullOrWhiteSpace(user.Uid))
        {
            // Auto sync upon user sign-in
            Task.Run(async () =>
            {
                await Task.Delay(500); // brief settling period
                await PerformFullSyncAsync();
            });
        }
        else
        {
            LastSyncTimestamp = 0;
            SetStatus(SyncStatus.SIGNED_OUT);
        }
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        if (IsNetworkAvailable() && _authService.IsLoggedIn)
        {
            Debug.WriteLine("[CloudSync] Network connectivity restored. Triggering sync...");
            Task.Run(async () =>
            {
                await Task.Delay(1500); // Allow interface routing table to settle
                await PerformFullSyncAsync();
            });
        }
    }

    private void OnDebounceTimerElapsed(object? state)
    {
        if (_authService.IsLoggedIn && IsNetworkAvailable())
        {
            _ = PerformFullSyncAsync();
        }
    }

    public static bool IsNetworkAvailable()
    {
        try
        {
            return NetworkInterface.GetIsNetworkAvailable();
        }
        catch
        {
            return true;
        }
    }

    public async Task<SyncResult> PerformFullSyncAsync(CancellationToken cancellationToken = default)
    {
        var user = _authService.CurrentUser;
        if (user == null || string.IsNullOrWhiteSpace(user.Uid))
        {
            SetStatus(SyncStatus.SIGNED_OUT);
            return new SyncResult { IsSuccess = false, ErrorMessage = "No user logged in" };
        }

        if (!IsNetworkAvailable())
        {
            var pending = await _localStore.GetPendingSyncItemsAsync();
            SetStatus(pending.Count > 0 ? SyncStatus.PENDING_CHANGES : SyncStatus.OFFLINE);
            return new SyncResult { IsSuccess = false, ErrorMessage = "Network is offline" };
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(45));

        // Single-flight lock: prevent multiple synchronization runs from corrupting state
        try
        {
            await _syncLock.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            LastErrorMessage = cancellationToken.IsCancellationRequested ? "Sync operation cancelled" : "Sync operation timed out";
            SetStatus(SyncStatus.ERROR);
            return new SyncResult { IsSuccess = false, ErrorMessage = LastErrorMessage };
        }

        try
        {
            SetStatus(SyncStatus.SYNCING);
            Debug.WriteLine($"[CloudSync] === Sync START === Trigger: FullSync, UID: {user.Uid}, LastSync: {LastSyncTimestamp}");

            // Step 1: PUSH local pending changes in safe dependency order
            var uploadRes = await UploadPendingChangesInternalAsync(user.Uid, timeoutCts.Token);
            Debug.WriteLine($"[CloudSync] Step 1 PUSH completed: Uploaded={uploadRes.UploadedCount}, Deleted={uploadRes.DeletedCount}");

            // Step 2: PULL remote changes in safe dependency order and reconcile per-entity (LWW)
            var downloadRes = await DownloadRemoteChangesInternalAsync(user.Uid, LastSyncTimestamp, timeoutCts.Token);
            Debug.WriteLine($"[CloudSync] Step 2 PULL completed: Downloaded={downloadRes.DownloadedCount}, Deleted={downloadRes.DeletedCount}, ConflictsResolved={downloadRes.ConflictsResolvedCount}");

            // Step 3: Reconcile Settings
            await SyncSettingsInternalAsync(user.Uid, timeoutCts.Token);
            Debug.WriteLine($"[CloudSync] Step 3 Settings reconciliation completed.");

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            LastSyncTimestamp = now;
            LastErrorMessage = null;

            var remainingPending = await _localStore.GetPendingSyncItemsAsync();
            _pendingCount = remainingPending.Count;

            SetStatus(_pendingCount > 0 ? SyncStatus.PENDING_CHANGES : SyncStatus.SUCCESS);

            var finalResult = new SyncResult
            {
                IsSuccess = true,
                UploadedCount = uploadRes.UploadedCount,
                DownloadedCount = downloadRes.DownloadedCount,
                DeletedCount = uploadRes.DeletedCount + downloadRes.DeletedCount,
                ConflictsResolvedCount = uploadRes.ConflictsResolvedCount + downloadRes.ConflictsResolvedCount,
                SyncedAt = now
            };
            Debug.WriteLine($"[CloudSync] === Sync COMPLETED === Result: Success, TotalUploaded: {finalResult.UploadedCount}, TotalDownloaded: {finalResult.DownloadedCount}, TotalDeleted: {finalResult.DeletedCount}");
            return finalResult;
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"[CloudSync] === Sync CANCELLED === UID: {user.Uid}");
            LastErrorMessage = cancellationToken.IsCancellationRequested ? "Sync operation cancelled" : "Sync operation timed out";
            SetStatus(SyncStatus.ERROR);
            return new SyncResult { IsSuccess = false, ErrorMessage = LastErrorMessage };
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            Debug.WriteLine($"[CloudSync] === Sync FAILED === UID: {user?.Uid}, Error: {ex.Message}");
            SetStatus(SyncStatus.ERROR);
            return new SyncResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
        finally
        {
            if (CurrentStatus == SyncStatus.SYNCING)
            {
                SetStatus(SyncStatus.ERROR);
            }
            _syncLock.Release();
        }
    }

    public async Task<SyncResult> UploadPendingChangesAsync(CancellationToken cancellationToken = default)
    {
        var user = _authService.CurrentUser;
        if (user == null || string.IsNullOrWhiteSpace(user.Uid))
            return new SyncResult { IsSuccess = false, ErrorMessage = "No user logged in" };

        if (!IsNetworkAvailable())
        {
            SetStatus(SyncStatus.OFFLINE);
            return new SyncResult { IsSuccess = false, ErrorMessage = "Network is offline" };
        }

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            SetStatus(SyncStatus.SYNCING);
            var res = await UploadPendingChangesInternalAsync(user.Uid, cancellationToken);
            SetStatus(SyncStatus.SUCCESS);
            return res;
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            SetStatus(SyncStatus.ERROR);
            return new SyncResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<SyncResult> DownloadRemoteChangesAsync(CancellationToken cancellationToken = default)
    {
        var user = _authService.CurrentUser;
        if (user == null || string.IsNullOrWhiteSpace(user.Uid))
            return new SyncResult { IsSuccess = false, ErrorMessage = "No user logged in" };

        if (!IsNetworkAvailable())
        {
            SetStatus(SyncStatus.OFFLINE);
            return new SyncResult { IsSuccess = false, ErrorMessage = "Network is offline" };
        }

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            SetStatus(SyncStatus.SYNCING);
            var res = await DownloadRemoteChangesInternalAsync(user.Uid, LastSyncTimestamp, cancellationToken);
            SetStatus(SyncStatus.SUCCESS);
            return res;
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            SetStatus(SyncStatus.ERROR);
            return new SyncResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task<SyncResult> UploadPendingChangesInternalAsync(string uid, CancellationToken cancellationToken)
    {
        var pending = await _localStore.GetPendingSyncItemsAsync();
        int uploaded = 0;
        int deleted = 0;

        if (pending.Count == 0)
        {
            return new SyncResult { IsSuccess = true, UploadedCount = 0, DeletedCount = 0 };
        }

        // Group items by normalized entity type
        var grouped = pending.GroupBy(p => LocalDataStore.NormalizeEntityType(p.EntityType))
                             .ToDictionary(g => g.Key, g => g.ToList());

        // Process in strict dependency order
        foreach (var entityType in PushOrder)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!grouped.TryGetValue(entityType, out var items))
                continue;

            string collection = GetCollectionName(entityType);
            var toUploadRecords = new List<CloudEntityRecord>();

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (item.Action == "DELETE")
                {
                    Debug.WriteLine($"[CloudSync] TOMBSTONE_PUSH {entityType} {item.EntityId}: local deletion newer ({item.Timestamp})");
                    await _firestoreClient.RecordTombstoneAsync(uid, collection, item.EntityId, item.Timestamp);
                    await _localStore.RemoveFromSyncQueueAsync(item.EntityType, item.EntityId);
                    deleted++;
                }
                else
                {
                    string? payload = await SerializeEntityAsync(entityType, item.EntityId);
                    if (!string.IsNullOrEmpty(payload))
                    {
                        long? localUpdatedAt = await _localStore.GetEntityUpdatedAtAsync(entityType, item.EntityId);
                        long updatedAt = localUpdatedAt ?? item.Timestamp;

                        toUploadRecords.Add(new CloudEntityRecord
                        {
                            Id = item.EntityId,
                            Collection = collection,
                            JsonPayload = payload,
                            UpdatedAt = updatedAt,
                            IsDeleted = false
                        });
                    }
                    else
                    {
                        // Entity was deleted or no longer exists locally; remove obsolete queue item
                        await _localStore.RemoveFromSyncQueueAsync(item.EntityType, item.EntityId);
                    }
                }
            }

            if (toUploadRecords.Count > 0)
            {
                int count = await _firestoreClient.UploadRecordsAsync(uid, collection, toUploadRecords);
                uploaded += count;
                foreach (var rec in toUploadRecords)
                {
                    Debug.WriteLine($"[CloudSync] PUSH {entityType} {rec.Id}: local newer ({rec.UpdatedAt})");
                    await _localStore.RemoveFromSyncQueueAsync(entityType, rec.Id);
                }
            }
        }

        var remaining = await _localStore.GetPendingSyncItemsAsync();
        _pendingCount = remaining.Count;

        return new SyncResult
        {
            IsSuccess = true,
            UploadedCount = uploaded,
            DeletedCount = deleted
        };
    }

    private async Task<SyncResult> DownloadRemoteChangesInternalAsync(string uid, long sinceTimestamp, CancellationToken cancellationToken)
    {
        int downloaded = 0;
        int deleted = 0;
        int conflictsResolved = 0;
        var downloadedAccounts = new Dictionary<string, long>();

        // Download and reconcile in strict dependency order
        foreach (var collection in PullOrder)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string entityType = GetEntityTypeFromCollection(collection);
            var records = await _firestoreClient.DownloadRecordsAsync(uid, collection, sinceTimestamp);
            Debug.WriteLine($"[CloudSync] Pulled {records.Count} records from collection '{collection}' for UID '{uid}'");

            if (records.Count > 0 && records.Any(r => !r.IsDeleted))
            {
                await _localStore.CleanupSampleDataIfPresentAsync();
            }

            foreach (var rec in records)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(rec.Id)) continue;

                var localTombstone = await _localStore.GetTombstoneAsync(entityType, rec.Id);
                long? localUpdatedAt = await _localStore.GetEntityUpdatedAtAsync(entityType, rec.Id);

                // Determine whether a transaction should adjust account balance
                bool shouldAdjustBalance = true;
                if (collection == "accounts" && !rec.IsDeleted)
                {
                    downloadedAccounts[rec.Id] = rec.UpdatedAt;
                }
                else if (collection == "transactions" && !rec.IsDeleted && !string.IsNullOrWhiteSpace(rec.JsonPayload))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(rec.JsonPayload);
                        if (doc.RootElement.TryGetProperty("sourceAccountId", out var srcProp) ||
                            doc.RootElement.TryGetProperty("SourceAccountId", out srcProp))
                        {
                            string? srcAccId = srcProp.GetString();
                            if (!string.IsNullOrEmpty(srcAccId) && downloadedAccounts.TryGetValue(srcAccId, out long accUpdatedAt))
                            {
                                if (accUpdatedAt >= rec.UpdatedAt)
                                {
                                    shouldAdjustBalance = false;
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (rec.IsDeleted)
                {
                    // REMOTE TOMBSTONE
                    long remoteDeletedAt = rec.DeletedAt ?? rec.UpdatedAt;

                    bool tombstoneAdjustBalance = true;
                    if (collection == "transactions")
                    {
                        var existingTx = await _localStore.GetTransactionByIdAsync(rec.Id);
                        if (existingTx != null && downloadedAccounts.TryGetValue(existingTx.SourceAccountId, out long accUpdatedAt))
                        {
                            if (accUpdatedAt >= remoteDeletedAt)
                            {
                                tombstoneAdjustBalance = false;
                            }
                        }
                    }

                    if (localUpdatedAt.HasValue)
                    {
                        if (remoteDeletedAt >= localUpdatedAt.Value)
                        {
                            // Remote deletion is newer or equal -> Remote deletion wins
                            Debug.WriteLine($"[CloudSync] DELETE {entityType} {rec.Id}: remote tombstone newer (remote={remoteDeletedAt} >= local={localUpdatedAt.Value})");
                            await DeleteEntityLocallyAsync(collection, rec.Id, adjustBalance: tombstoneAdjustBalance);
                            await _localStore.RecordTombstoneAsync(entityType, rec.Id, remoteDeletedAt, markForSync: false);
                            deleted++;
                        }
                        else
                        {
                            // Local edit occurred AFTER remote deletion -> Local modification wins (resurrect local)
                            Debug.WriteLine($"[CloudSync] RESURRECT {entityType} {rec.Id}: local modification newer than remote deletion (local={localUpdatedAt.Value} > remote={remoteDeletedAt})");
                            await _localStore.ClearTombstoneAsync(entityType, rec.Id);
                            _localStore.EnqueueSync(entityType, rec.Id, "UPSERT", localUpdatedAt.Value);
                            conflictsResolved++;
                        }
                    }
                    else
                    {
                        // Entity is not present locally; record tombstone metadata and ensure cleanly purged
                        await DeleteEntityLocallyAsync(collection, rec.Id, adjustBalance: false);
                        await _localStore.RecordTombstoneAsync(entityType, rec.Id, remoteDeletedAt, markForSync: false);
                    }
                }
                else
                {
                    // REMOTE ACTIVE RECORD
                    if (localTombstone != null)
                    {
                        if (localTombstone.DeletedAt >= rec.UpdatedAt)
                        {
                            // Local deletion is newer or equal -> Local deletion wins, prevent resurrection!
                            Debug.WriteLine($"[CloudSync] SKIP_TOMBSTONE {entityType} {rec.Id}: local deletion newer (tombstone={localTombstone.DeletedAt} >= remote={rec.UpdatedAt})");
                            // Re-queue local tombstone to push to cloud
                            _localStore.EnqueueSync(entityType, rec.Id, "DELETE", localTombstone.DeletedAt);
                            conflictsResolved++;
                        }
                        else
                        {
                            // Remote edit occurred AFTER local deletion -> Remote edit wins (resurrect from remote)
                            Debug.WriteLine($"[CloudSync] RESURRECT_PULL {entityType} {rec.Id}: remote update newer than local deletion (remote={rec.UpdatedAt} > tombstone={localTombstone.DeletedAt})");
                            await _localStore.ClearTombstoneAsync(entityType, rec.Id);
                            if (!string.IsNullOrWhiteSpace(rec.JsonPayload))
                            {
                                await UpsertEntityLocallyAsync(collection, rec.JsonPayload, adjustBalance: shouldAdjustBalance);
                                downloaded++;
                            }
                            conflictsResolved++;
                        }
                    }
                    else if (localUpdatedAt.HasValue)
                    {
                        if (rec.UpdatedAt > localUpdatedAt.Value)
                        {
                            // CLOUD NEWER: remote.updatedAt > local.updatedAt -> PULL
                            Debug.WriteLine($"[CloudSync] PULL {entityType} {rec.Id}: remote newer (remote={rec.UpdatedAt} > local={localUpdatedAt.Value})");
                            if (!string.IsNullOrWhiteSpace(rec.JsonPayload))
                            {
                                await UpsertEntityLocallyAsync(collection, rec.JsonPayload, adjustBalance: shouldAdjustBalance);
                                await _localStore.RemoveFromSyncQueueAsync(entityType, rec.Id);
                                downloaded++;
                            }
                        }
                        else if (localUpdatedAt.Value > rec.UpdatedAt)
                        {
                            // WINDOWS LOCAL NEWER: local.updatedAt > remote.updatedAt -> PUSH
                            Debug.WriteLine($"[CloudSync] PUSH {entityType} {rec.Id}: local newer (local={localUpdatedAt.Value} > remote={rec.UpdatedAt})");
                            _localStore.EnqueueSync(entityType, rec.Id, "UPSERT", localUpdatedAt.Value);
                            conflictsResolved++;
                        }
                        else
                        {
                            // SAME VERSION: local.updatedAt == remote.updatedAt -> no action
                            Debug.WriteLine($"[CloudSync] SKIP {entityType} {rec.Id}: versions equal ({localUpdatedAt.Value})");
                        }
                    }
                    else
                    {
                        // REMOTE ONLY: entity does not exist locally -> PULL into Windows
                        Debug.WriteLine($"[CloudSync] PULL_NEW {entityType} {rec.Id}: remote only ({rec.UpdatedAt})");
                        if (!string.IsNullOrWhiteSpace(rec.JsonPayload))
                        {
                            await UpsertEntityLocallyAsync(collection, rec.JsonPayload, adjustBalance: shouldAdjustBalance);
                            downloaded++;
                        }
                    }
                }
            }
        }

        return new SyncResult
        {
            IsSuccess = true,
            DownloadedCount = downloaded,
            DeletedCount = deleted,
            ConflictsResolvedCount = conflictsResolved
        };
    }

    private async Task SyncSettingsInternalAsync(string uid, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var localSettings = await _localStore.GetSettingsAsync();
        var remoteRecords = await _firestoreClient.DownloadRecordsAsync(uid, CloudSettings.SettingsCollection, 0);
        var appSettingDoc = remoteRecords.FirstOrDefault(r => r.Id == CloudSettings.SettingsDocumentId && !r.IsDeleted);

        if (appSettingDoc != null && !string.IsNullOrWhiteSpace(appSettingDoc.JsonPayload))
        {
            var remoteSettings = JsonSerializer.Deserialize<CloudSettings>(appSettingDoc.JsonPayload, JsonOpts);
            if (remoteSettings != null)
            {
                if (remoteSettings.UpdatedAt > localSettings.UpdatedAt)
                {
                    // Remote settings are newer -> apply locally
                    Debug.WriteLine($"[CloudSync] PULL settings {CloudSettings.SettingsDocumentId}: remote newer ({remoteSettings.UpdatedAt} > {localSettings.UpdatedAt})");
                    await _localStore.SaveSettingsAsync(remoteSettings, markForSync: false);
                    await _localStore.RemoveFromSyncQueueAsync("SETTINGS", CloudSettings.SettingsDocumentId);
                    return;
                }
                else if (localSettings.UpdatedAt > remoteSettings.UpdatedAt)
                {
                    // Local settings are newer -> push to cloud
                    Debug.WriteLine($"[CloudSync] PUSH settings {CloudSettings.SettingsDocumentId}: local newer ({localSettings.UpdatedAt} > {remoteSettings.UpdatedAt})");
                }
                else
                {
                    // Identical settings versions -> no action
                    Debug.WriteLine($"[CloudSync] SKIP settings {CloudSettings.SettingsDocumentId}: versions equal ({localSettings.UpdatedAt})");
                    await _localStore.RemoveFromSyncQueueAsync("SETTINGS", CloudSettings.SettingsDocumentId);
                    return;
                }
            }
        }

        // Push local settings to cloud
        string payload = JsonSerializer.Serialize(localSettings);
        var record = new CloudEntityRecord
        {
            Id = CloudSettings.SettingsDocumentId,
            Collection = CloudSettings.SettingsCollection,
            JsonPayload = payload,
            UpdatedAt = localSettings.UpdatedAt
        };
        await _firestoreClient.UploadRecordsAsync(uid, CloudSettings.SettingsCollection, new List<CloudEntityRecord> { record });
        await _localStore.RemoveFromSyncQueueAsync("SETTINGS", CloudSettings.SettingsDocumentId);
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
        return LocalDataStore.NormalizeEntityType(entityType) switch
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

    private async Task UpsertEntityLocallyAsync(string collection, string jsonPayload, bool adjustBalance)
    {
        try
        {
            switch (collection)
            {
                case "accounts":
                    if (JsonSerializer.Deserialize<Account>(jsonPayload, JsonOpts) is { } acc)
                        await _localStore.UpsertAccountAsync(acc, markForSync: false);
                    break;
                case "transactions":
                    if (JsonSerializer.Deserialize<Transaction>(jsonPayload, JsonOpts) is { } tx)
                        await _localStore.UpsertTransactionAsync(tx, markForSync: false, adjustBalance: adjustBalance);
                    break;
                case "categories":
                    if (JsonSerializer.Deserialize<Category>(jsonPayload, JsonOpts) is { } cat)
                        await _localStore.UpsertCategoryAsync(cat, markForSync: false);
                    break;
                case "budgets":
                    if (JsonSerializer.Deserialize<Budget>(jsonPayload, JsonOpts) is { } b)
                        await _localStore.UpsertBudgetAsync(b, markForSync: false);
                    break;
                case "recurring_rules":
                    if (JsonSerializer.Deserialize<RecurringTransaction>(jsonPayload, JsonOpts) is { } r)
                        await _localStore.UpsertRecurringRuleAsync(r, markForSync: false);
                    break;
                case "goals":
                    if (JsonSerializer.Deserialize<FinancialGoal>(jsonPayload, JsonOpts) is { } g)
                        await _localStore.UpsertGoalAsync(g, markForSync: false);
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CloudSync] Deserialization failure for collection {collection}: {ex.Message}");
        }
    }

    private async Task DeleteEntityLocallyAsync(string collection, string id, bool adjustBalance)
    {
        switch (collection)
        {
            case "accounts":
                await _localStore.DeleteAccountAsync(id, markForSync: false);
                break;
            case "transactions":
                await _localStore.DeleteTransactionAsync(id, markForSync: false, adjustBalance: adjustBalance);
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

    private static string GetCollectionName(string entityType)
    {
        return LocalDataStore.NormalizeEntityType(entityType) switch
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

    private static string GetEntityTypeFromCollection(string collection)
    {
        return collection.ToLowerInvariant() switch
        {
            "accounts" => "ACCOUNT",
            "categories" => "CATEGORY",
            "transactions" => "TRANSACTION",
            "budgets" => "BUDGET",
            "recurring_rules" => "RECURRING_RULE",
            "goals" => "GOAL",
            "settings" => "SETTINGS",
            _ => collection.ToUpperInvariant()
        };
    }

    private void SetStatus(SyncStatus status)
    {
        CurrentStatus = status;
        SyncStatusChanged?.Invoke(this, status);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _debounceTimer.Dispose();
        _syncLock.Dispose();

        _localStore.DataChanged -= OnLocalDataChanged;
        _authService.AuthStateChanged -= OnAuthStateChanged;
        try
        {
            NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        }
        catch { }
    }
}
