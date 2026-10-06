using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using FinPulse.Windows.Services;

namespace FinPulse.Windows.ViewModels;

public class ShellViewModel : ViewModelBase
{
    private readonly ISyncService _syncService;
    private readonly IAuthService _authService;

    private string _currentNavTag = "overview";
    public string CurrentNavTag
    {
        get => _currentNavTag;
        set => SetProperty(ref _currentNavTag, value);
    }

    private string _syncStatusBadge = LocalizationService.Current.GetString("Shell_Sync_OK", "Sync: OK");
    public string SyncStatusBadge
    {
        get => _syncStatusBadge;
        set => SetProperty(ref _syncStatusBadge, value);
    }

    public IAsyncRelayCommand SyncNowCommand { get; }

    public ShellViewModel(ISyncService syncService, IAuthService authService)
    {
        _syncService = syncService;
        _authService = authService;

        SyncNowCommand = new AsyncRelayCommand(async () =>
        {
            await _syncService.PerformFullSyncAsync();
        });

        _syncService.SyncStatusChanged += (s, status) =>
        {
            SyncStatusBadge = status switch
            {
                SyncStatus.SYNCING => LocalizationService.Current.GetString("Shell_Sync_Syncing", "Syncing..."),
                SyncStatus.ERROR => LocalizationService.Current.GetString("Shell_Sync_Error", "Sync Error"),
                _ => LocalizationService.Current.GetString("Shell_Sync_OK", "Sync: OK")
            };
        };
    }
}
