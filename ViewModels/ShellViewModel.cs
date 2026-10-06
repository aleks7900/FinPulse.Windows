using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using FinPulse.Windows.Services;

namespace FinPulse.Windows.ViewModels;

public class ShellViewModel : ViewModelBase
{
    private readonly ISyncService _syncService;
    private readonly IAuthService _authService;
    private readonly ICloudSyncCoordinator _syncCoordinator;

    private static readonly SolidColorBrush GreenBrush = new(Color.FromArgb(255, 16, 124, 65));
    private static readonly SolidColorBrush RedBrush = new(Color.FromArgb(255, 196, 43, 28));
    private static readonly SolidColorBrush OrangeBrush = new(Color.FromArgb(255, 202, 80, 16));
    private static readonly SolidColorBrush MutedBrush = new(Color.FromArgb(255, 140, 140, 140));
    private static readonly SolidColorBrush DefaultBrush = new(Color.FromArgb(255, 32, 32, 32));

    private string _currentNavTag = "overview";
    public string CurrentNavTag
    {
        get => _currentNavTag;
        set => SetProperty(ref _currentNavTag, value);
    }

    private string _syncStatusBadge = "Sync: OK";
    public string SyncStatusBadge
    {
        get => _syncStatusBadge;
        set => SetProperty(ref _syncStatusBadge, value);
    }

    public ICloudSyncCoordinator Coordinator => _syncCoordinator;
    public GlobalSyncState SyncState => _syncCoordinator.State;
    public bool IsSyncing => _syncCoordinator.IsSyncing;
    public bool IsNotSyncing => !_syncCoordinator.IsSyncing;
    public bool CanTriggerSync => !_syncCoordinator.IsSyncing;
    public Microsoft.UI.Xaml.Visibility SyncProgressVisibility => IsSyncing ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public Microsoft.UI.Xaml.Visibility SyncIconVisibility => IsSyncing ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;

    public string GlobalSyncStatusLabel => SyncState switch
    {
        GlobalSyncState.Syncing => LocalizationService.Current.GetString("Shell_Sync_Syncing", "Syncing..."),
        GlobalSyncState.Success => LocalizationService.Current.GetString("Sync_Status_Synchronized", "Synced"),
        GlobalSyncState.Error => LocalizationService.Current.GetString("Shell_Sync_Error", "Sync Error"),
        GlobalSyncState.Offline => LocalizationService.Current.GetString("Sync_Status_Offline", "Offline"),
        GlobalSyncState.AuthenticationRequired => LocalizationService.Current.GetString("Sync_Status_SignIn", "Sign In"),
        _ => LocalizationService.Current.GetString("Action_Cloud_Sync", "Sync")
    };

    public string GlobalSyncIconGlyph => SyncState switch
    {
        GlobalSyncState.Syncing => "\uE895", // Sync
        GlobalSyncState.Success => "\uE73E", // Checkmark
        GlobalSyncState.Error => "\uE783",   // Warning/Error
        GlobalSyncState.Offline => "\uE7BA", // CloudOff / Blocked
        GlobalSyncState.AuthenticationRequired => "\uE77B", // Contact
        _ => "\uE753" // Cloud
    };

    public SolidColorBrush GlobalSyncIconBrush => SyncState switch
    {
        GlobalSyncState.Syncing => GreenBrush,
        GlobalSyncState.Success => GreenBrush,
        GlobalSyncState.Error => RedBrush,
        GlobalSyncState.Offline => OrangeBrush,
        GlobalSyncState.AuthenticationRequired => MutedBrush,
        _ => MutedBrush
    };

    public SolidColorBrush GlobalSyncLabelBrush => SyncState switch
    {
        GlobalSyncState.Syncing => GreenBrush,
        GlobalSyncState.Success => GreenBrush,
        GlobalSyncState.Error => RedBrush,
        GlobalSyncState.Offline => OrangeBrush,
        GlobalSyncState.AuthenticationRequired => MutedBrush,
        _ => MutedBrush
    };

    public string GlobalSyncTooltip
    {
        get
        {
            var loc = LocalizationService.Current;
            return SyncState switch
            {
                GlobalSyncState.Syncing => loc.GetString("Sync_Tooltip_Syncing", "Synchronizing with Firebase Cloud..."),
                GlobalSyncState.Success => loc.GetString("Sync_Tooltip_Success", "Synchronized with Firebase Cloud"),
                GlobalSyncState.Error => string.Format(loc.GetString("Sync_Tooltip_Error", "Sync Error: {0}"), _syncCoordinator.LastError ?? "Unknown error"),
                GlobalSyncState.Offline => loc.GetString("Sync_Tooltip_Offline", "Device is offline. Changes will sync when connected."),
                GlobalSyncState.AuthenticationRequired => loc.GetString("Sync_Tooltip_AuthRequired", "Sign in with Google to enable cloud sync"),
                _ => _syncCoordinator.LastSyncTimestamp > 0
                    ? string.Format(loc.GetString("Sync_Tooltip_LastSynced", "Last synced: {0}"),
                        DateTimeOffset.FromUnixTimeMilliseconds(_syncCoordinator.LastSyncTimestamp).LocalDateTime.ToString("g", CultureInfo.CurrentCulture))
                    : loc.GetString("Sync_Tooltip_ClickToSync", "Click to synchronize now")
            };
        }
    }

    private bool _isFeedbackTipOpen;
    public bool IsFeedbackTipOpen
    {
        get => _isFeedbackTipOpen;
        set => SetProperty(ref _isFeedbackTipOpen, value);
    }

    private string _feedbackTipTitle = "Cloud Sync";
    public string FeedbackTipTitle
    {
        get => _feedbackTipTitle;
        set => SetProperty(ref _feedbackTipTitle, value);
    }

    private string _feedbackTipSubtitle = string.Empty;
    public string FeedbackTipSubtitle
    {
        get => _feedbackTipSubtitle;
        set => SetProperty(ref _feedbackTipSubtitle, value);
    }

    private InfoBarSeverity _feedbackTipSeverity = InfoBarSeverity.Informational;
    public InfoBarSeverity FeedbackTipSeverity
    {
        get => _feedbackTipSeverity;
        set => SetProperty(ref _feedbackTipSeverity, value);
    }

    private CancellationTokenSource? _feedbackCts;

    public IAsyncRelayCommand TriggerGlobalSyncCommand { get; }
    public IAsyncRelayCommand SyncNowCommand { get; }

    public ShellViewModel(
        ISyncService syncService,
        IAuthService authService,
        ICloudSyncCoordinator syncCoordinator)
    {
        _syncService = syncService;
        _authService = authService;
        _syncCoordinator = syncCoordinator;

        SyncNowCommand = new AsyncRelayCommand(async () =>
        {
            await _syncCoordinator.SyncAsync(SyncTrigger.Manual);
        });

        TriggerGlobalSyncCommand = new AsyncRelayCommand(ExecuteGlobalSyncActionAsync);

        _syncCoordinator.StateChanged += (s, state) =>
        {
            UpdateAllSyncProperties();
        };

        _syncService.SyncStatusChanged += (s, status) =>
        {
            UpdateAllSyncProperties();
        };

        LocalizationService.Current.LanguageChanged += (s, e) =>
        {
            UpdateAllSyncProperties();
            OnPropertyChanged(nameof(GlobalSyncStatusLabel));
            OnPropertyChanged(nameof(GlobalSyncTooltip));
            OnPropertyChanged(nameof(SyncStatusBadge));
        };
    }

    private async Task ExecuteGlobalSyncActionAsync()
    {
        var loc = LocalizationService.Current;

        if (SyncState == GlobalSyncState.AuthenticationRequired)
        {
            ShowFeedback(
                loc.GetString("Shell_Sync_Title", "Cloud Sync"),
                loc.GetString("Sync_Feedback_AuthRequired", "Sign in with Google to enable cloud sync"),
                InfoBarSeverity.Warning);

            App.NavigateTo("settings");
            return;
        }

        if (SyncState == GlobalSyncState.Offline)
        {
            ShowFeedback(
                loc.GetString("Shell_Sync_Title", "Cloud Sync"),
                loc.GetString("Sync_Feedback_Offline", "No internet connection"),
                InfoBarSeverity.Warning);
            return;
        }

        if (IsSyncing) return;

        var result = await _syncCoordinator.SyncAsync(SyncTrigger.Manual);
        if (result.IsSuccess)
        {
            ShowFeedback(
                loc.GetString("Shell_Sync_Title", "Cloud Sync"),
                loc.GetString("Sync_Feedback_Success", "Cloud sync completed"),
                InfoBarSeverity.Success);
        }
        else
        {
            ShowFeedback(
                loc.GetString("Shell_Sync_Title", "Cloud Sync"),
                result.ErrorMessage ?? loc.GetString("Sync_Feedback_Failed", "Cloud sync failed"),
                InfoBarSeverity.Error);
        }
    }

    public void ShowFeedback(string title, string subtitle, InfoBarSeverity severity = InfoBarSeverity.Informational)
    {
        _feedbackCts?.Cancel();
        _feedbackCts = new CancellationTokenSource();
        var token = _feedbackCts.Token;

        FeedbackTipTitle = title;
        FeedbackTipSubtitle = subtitle;
        FeedbackTipSeverity = severity;
        IsFeedbackTipOpen = true;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(3500, token);
                if (!token.IsCancellationRequested)
                {
                    App.RootWindow?.DispatcherQueue.TryEnqueue(() =>
                    {
                        IsFeedbackTipOpen = false;
                    });
                }
            }
            catch { }
        });
    }

    private void UpdateAllSyncProperties()
    {
        OnPropertyChanged(nameof(SyncState));
        OnPropertyChanged(nameof(IsSyncing));
        OnPropertyChanged(nameof(IsNotSyncing));
        OnPropertyChanged(nameof(CanTriggerSync));
        OnPropertyChanged(nameof(SyncProgressVisibility));
        OnPropertyChanged(nameof(SyncIconVisibility));
        OnPropertyChanged(nameof(GlobalSyncStatusLabel));
        OnPropertyChanged(nameof(GlobalSyncIconGlyph));
        OnPropertyChanged(nameof(GlobalSyncIconBrush));
        OnPropertyChanged(nameof(GlobalSyncLabelBrush));
        OnPropertyChanged(nameof(GlobalSyncTooltip));

        SyncStatusBadge = _syncService.CurrentStatus switch
        {
            SyncStatus.SYNCING => LocalizationService.Current.GetString("Shell_Sync_Syncing", "Syncing..."),
            SyncStatus.ERROR => LocalizationService.Current.GetString("Shell_Sync_Error", "Sync Error"),
            _ => LocalizationService.Current.GetString("Shell_Sync_OK", "Sync: OK")
        };
    }
}
