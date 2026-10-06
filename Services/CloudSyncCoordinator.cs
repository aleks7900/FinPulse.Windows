using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using FinPulse.Windows.Models;

namespace FinPulse.Windows.Services;

public class CloudSyncCoordinator : ICloudSyncCoordinator
{
    private readonly ISyncService _syncService;
    private readonly IAuthService _authService;
    private readonly ILocalDataStore _localStore;

    private readonly object _syncLock = new();
    private Task<SyncResult>? _activeSyncTask;

    private readonly CancellationTokenSource _cts = new();
    private Timer? _periodicTimer;
    private bool _isDisposed;

    private int _startupSyncExecuted = 0;
    private bool _isSyncing;
    private DateTimeOffset? _recentSuccessTime;
    private string? _lastError;
    private SyncPreferences _preferences = new();

    public GlobalSyncState State { get; private set; } = GlobalSyncState.Idle;
    public bool IsSyncing => _isSyncing;
    public long LastSyncTimestamp => _syncService.LastSyncTimestamp;
    public DateTimeOffset? LastSyncTime => _syncService.LastSyncTimestamp > 0
        ? DateTimeOffset.FromUnixTimeMilliseconds(_syncService.LastSyncTimestamp)
        : null;

    public int PendingChangesCount => _syncService.PendingChangesCount;
    public string? LastError => _lastError ?? _syncService.LastErrorMessage;

    public bool IsAutoSyncEnabled => _preferences.IsAutoSyncEnabled;
    public bool IsSyncOnStartupEnabled => _preferences.IsSyncOnStartupEnabled;
    public long SyncIntervalMinutes => _preferences.SyncIntervalMinutes;

    public event EventHandler<GlobalSyncState>? StateChanged;

    public CloudSyncCoordinator(
        ISyncService syncService,
        IAuthService authService,
        ILocalDataStore localStore)
    {
        _syncService = syncService;
        _authService = authService;
        _localStore = localStore;

        _periodicTimer = new Timer(OnPeriodicTimerFired, null, Timeout.Infinite, Timeout.Infinite);

        _authService.AuthStateChanged += OnAuthStateChanged;
        _syncService.SyncStatusChanged += OnSyncStatusChanged;
        _localStore.DataChanged += OnLocalDataChanged;

        try
        {
            NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
        }
        catch { }

        RecomputeState();
    }

    public async Task InitializeAsync()
    {
        try
        {
            _preferences = await _localStore.GetSyncPreferencesAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CloudSyncCoordinator] Error loading sync preferences: {ex.Message}");
            _preferences = new SyncPreferences();
        }

        if (_preferences.IsAutoSyncEnabled && _authService.IsLoggedIn)
        {
            SchedulePeriodic(_preferences.SyncIntervalMinutes);
        }

        RecomputeState();
    }

    private void OnAuthStateChanged(object? sender, UserSession? user)
    {
        if (user != null && !string.IsNullOrWhiteSpace(user.Uid) && user.Uid != "local_default_user")
        {
            if (_preferences.IsAutoSyncEnabled)
            {
                SchedulePeriodic(_preferences.SyncIntervalMinutes);
            }
        }
        else
        {
            CancelPeriodic();
            _recentSuccessTime = null;
        }
        RecomputeState();
    }

    private void OnSyncStatusChanged(object? sender, SyncStatus status)
    {
        RecomputeState();
    }

    private void OnLocalDataChanged(object? sender, EventArgs e)
    {
        RecomputeState();
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        RecomputeState();
    }

    private void OnPeriodicTimerFired(object? state)
    {
        if (_isDisposed || _cts.IsCancellationRequested) return;

        if (_preferences.IsAutoSyncEnabled &&
            _authService.IsLoggedIn &&
            CloudSyncService.IsNetworkAvailable() &&
            !_isSyncing)
        {
            Debug.WriteLine("[CloudSyncCoordinator] Periodic timer fired. Triggering sync...");
            _ = SyncAsync(SyncTrigger.Scheduled, _cts.Token);
        }
    }

    private void SchedulePeriodic(long intervalMinutes)
    {
        if (_isDisposed) return;
        long safeMinutes = Math.Max(15L, intervalMinutes);
        TimeSpan interval = TimeSpan.FromMinutes(safeMinutes);
        _periodicTimer?.Change(interval, interval);
        Debug.WriteLine($"[CloudSyncCoordinator] Scheduled periodic sync every {safeMinutes} minutes.");
    }

    private void CancelPeriodic()
    {
        if (_isDisposed) return;
        _periodicTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        Debug.WriteLine("[CloudSyncCoordinator] Cancelled periodic sync.");
    }

    public async Task<SyncResult> SyncAsync(SyncTrigger trigger = SyncTrigger.Manual, CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            return new SyncResult { IsSuccess = false, ErrorMessage = "Coordinator disposed" };
        }

        // 1. Check Authentication
        if (!_authService.IsLoggedIn)
        {
            _lastError = "Sign in with Google to enable cloud sync";
            RecomputeState();
            return new SyncResult { IsSuccess = false, ErrorMessage = _lastError };
        }

        // 2. Check Network
        if (!CloudSyncService.IsNetworkAvailable())
        {
            _lastError = "No internet connection";
            RecomputeState();
            return new SyncResult { IsSuccess = false, ErrorMessage = _lastError };
        }

        // 3. Single-flight concurrency control: reuse active task if one is already running
        Task<SyncResult> taskToAwait;
        lock (_syncLock)
        {
            if (_activeSyncTask != null && !_activeSyncTask.IsCompleted)
            {
                Debug.WriteLine($"[CloudSyncCoordinator] Reusing active sync for trigger: {trigger}");
                taskToAwait = _activeSyncTask;
            }
            else
            {
                taskToAwait = ExecuteSyncInternalAsync(trigger, cancellationToken);
                _activeSyncTask = taskToAwait;
            }
        }

        return await taskToAwait;
    }

    private async Task<SyncResult> ExecuteSyncInternalAsync(SyncTrigger trigger, CancellationToken cancellationToken)
    {
        _isSyncing = true;
        _lastError = null;
        RecomputeState();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

        try
        {
            Debug.WriteLine($"[CloudSyncCoordinator] Starting sync triggered by: {trigger}");
            var result = await _syncService.PerformFullSyncAsync(linkedCts.Token);

            if (result.IsSuccess)
            {
                _recentSuccessTime = DateTimeOffset.UtcNow;
                _lastError = null;
                Debug.WriteLine($"[CloudSyncCoordinator] Sync succeeded for trigger: {trigger}");

                // Keep success visual feedback active for 3.5 seconds
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(3500, _cts.Token);
                        _recentSuccessTime = null;
                        RecomputeState();
                    }
                    catch { }
                });
            }
            else
            {
                _lastError = result.ErrorMessage;
                Debug.WriteLine($"[CloudSyncCoordinator] Sync failed for trigger: {trigger}: {_lastError}");
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            _lastError = "Sync cancelled";
            return new SyncResult { IsSuccess = false, ErrorMessage = _lastError };
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            return new SyncResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
        finally
        {
            lock (_syncLock)
            {
                _activeSyncTask = null;
            }
            _isSyncing = false;
            RecomputeState();
        }
    }

    public void TriggerStartupSync()
    {
        if (Interlocked.CompareExchange(ref _startupSyncExecuted, 1, 0) == 0)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    if (!_preferences.IsAutoSyncEnabled || !_preferences.IsSyncOnStartupEnabled)
                    {
                        Debug.WriteLine("[CloudSyncCoordinator] Startup sync skipped: disabled in settings");
                        return;
                    }

                    if (!_authService.IsLoggedIn)
                    {
                        Debug.WriteLine("[CloudSyncCoordinator] Startup sync skipped: user not authenticated");
                        return;
                    }

                    if (!CloudSyncService.IsNetworkAvailable())
                    {
                        Debug.WriteLine("[CloudSyncCoordinator] Startup sync skipped: device offline");
                        return;
                    }

                    Debug.WriteLine("[CloudSyncCoordinator] Executing startup sync...");
                    await SyncAsync(SyncTrigger.Startup, _cts.Token);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CloudSyncCoordinator] Startup sync error: {ex.Message}");
                }
            });
        }
        else
        {
            Debug.WriteLine("[CloudSyncCoordinator] Startup sync already executed for this process, ignoring subsequent call.");
        }
    }

    public void TriggerForegroundSync()
    {
        if (!_preferences.IsAutoSyncEnabled || !_authService.IsLoggedIn || !CloudSyncService.IsNetworkAvailable())
        {
            return;
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (now - _syncService.LastSyncTimestamp > 45000 || _syncService.PendingChangesCount > 0)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await SyncAsync(SyncTrigger.Foreground, _cts.Token);
                }
                catch { }
            });
        }
    }

    public async Task SetAutoSyncEnabledAsync(bool enabled)
    {
        _preferences.IsAutoSyncEnabled = enabled;
        await _localStore.SaveSyncPreferencesAsync(_preferences);

        if (enabled && _authService.IsLoggedIn)
        {
            SchedulePeriodic(_preferences.SyncIntervalMinutes);
        }
        else
        {
            CancelPeriodic();
        }

        RecomputeState();
    }

    public async Task SetSyncOnStartupEnabledAsync(bool enabled)
    {
        _preferences.IsSyncOnStartupEnabled = enabled;
        await _localStore.SaveSyncPreferencesAsync(_preferences);
    }

    public async Task SetSyncIntervalMinutesAsync(long minutes)
    {
        long safeMinutes = Math.Max(15L, minutes);
        _preferences.SyncIntervalMinutes = safeMinutes;
        await _localStore.SaveSyncPreferencesAsync(_preferences);

        if (_preferences.IsAutoSyncEnabled && _authService.IsLoggedIn)
        {
            SchedulePeriodic(safeMinutes);
        }
    }

    private void RecomputeState()
    {
        var newState = ComputeCurrentState();
        if (State != newState)
        {
            State = newState;
            StateChanged?.Invoke(this, newState);
        }
    }

    private GlobalSyncState ComputeCurrentState()
    {
        if (!_authService.IsLoggedIn)
        {
            return GlobalSyncState.AuthenticationRequired;
        }

        if (!CloudSyncService.IsNetworkAvailable())
        {
            return GlobalSyncState.Offline;
        }

        if (_isSyncing || _syncService.CurrentStatus == SyncStatus.SYNCING)
        {
            return GlobalSyncState.Syncing;
        }

        if (_syncService.CurrentStatus == SyncStatus.ERROR)
        {
            return GlobalSyncState.Error;
        }

        if (_recentSuccessTime != null && (DateTimeOffset.UtcNow - _recentSuccessTime.Value).TotalMilliseconds < 3500)
        {
            return GlobalSyncState.Success;
        }

        if (_syncService.CurrentStatus == SyncStatus.SUCCESS)
        {
            return GlobalSyncState.Success;
        }

        return GlobalSyncState.Idle;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        try
        {
            _cts.Cancel();
            _cts.Dispose();
        }
        catch { }

        _periodicTimer?.Dispose();
        _periodicTimer = null;

        _authService.AuthStateChanged -= OnAuthStateChanged;
        _syncService.SyncStatusChanged -= OnSyncStatusChanged;
        _localStore.DataChanged -= OnLocalDataChanged;

        try
        {
            NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        }
        catch { }
    }
}
