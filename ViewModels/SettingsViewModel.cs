using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using FinPulse.Windows.Models;
using FinPulse.Windows.Services;

namespace FinPulse.Windows.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly ILocalDataStore _store;
    private readonly ISyncService _syncService;
    private readonly IAuthService _authService;
    private readonly ICloudSyncCoordinator _syncCoordinator;

    public ObservableCollection<CurrencyMetadata> AvailableCurrencies { get; } = new();
    public ObservableCollection<LanguageOption> AvailableLanguages { get; } = new();
    public ObservableCollection<SyncIntervalOption> AvailableSyncIntervals { get; } = new();

    private SyncIntervalOption? _selectedSyncInterval;
    public SyncIntervalOption? SelectedSyncInterval
    {
        get => _selectedSyncInterval;
        set
        {
            if (SetProperty(ref _selectedSyncInterval, value) && value != null)
            {
                _ = _syncCoordinator.SetSyncIntervalMinutesAsync(value.Minutes);
            }
        }
    }

    public bool IsAutoSyncEnabled
    {
        get => _syncCoordinator.IsAutoSyncEnabled;
        set
        {
            if (_syncCoordinator.IsAutoSyncEnabled != value)
            {
                _ = _syncCoordinator.SetAutoSyncEnabledAsync(value);
                OnPropertyChanged();
            }
        }
    }

    public bool IsSyncOnStartupEnabled
    {
        get => _syncCoordinator.IsSyncOnStartupEnabled;
        set
        {
            if (_syncCoordinator.IsSyncOnStartupEnabled != value)
            {
                _ = _syncCoordinator.SetSyncOnStartupEnabledAsync(value);
                OnPropertyChanged();
            }
        }
    }

    public string ConnectedAccountEmail => CurrentUser?.Email ?? LocalizationService.Current.GetString("Settings_NotSignedIn", "Not Signed In");

    private CurrencyMetadata? _selectedCurrency;
    public CurrencyMetadata? SelectedCurrency
    {
        get => _selectedCurrency;
        set
        {
            if (SetProperty(ref _selectedCurrency, value) && value != null)
            {
                _ = UpdateBaseCurrencyAsync(value.Code);
            }
        }
    }

    private LanguageOption? _selectedLanguage;
    public LanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value) && value != null)
            {
                _ = UpdateLanguageAsync(value.Code);
            }
        }
    }

    private string _selectedTheme = "System";
    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                _ = UpdateThemeAsync(value);
            }
        }
    }

    private bool _hideBalances = false;
    public bool HideBalances
    {
        get => _hideBalances;
        set
        {
            if (SetProperty(ref _hideBalances, value))
            {
                _ = UpdateHideBalancesAsync(value);
            }
        }
    }

    private string _syncStatusText = "Idle";
    public string SyncStatusText
    {
        get => _syncStatusText;
        set => SetProperty(ref _syncStatusText, value);
    }

    private string _lastSyncText = "Never";
    public string LastSyncText
    {
        get => _lastSyncText;
        set => SetProperty(ref _lastSyncText, value);
    }

    private int _pendingChanges;
    public int PendingChanges
    {
        get => _pendingChanges;
        set => SetProperty(ref _pendingChanges, value);
    }

    private UserSession? _currentUser;
    public UserSession? CurrentUser
    {
        get => _currentUser;
        set
        {
            if (SetProperty(ref _currentUser, value))
            {
                OnPropertyChanged(nameof(IsLoggedIn));
                OnPropertyChanged(nameof(IsNotLoggedIn));
            }
        }
    }

    public bool IsLoggedIn => CurrentUser != null && !string.IsNullOrWhiteSpace(CurrentUser.Uid) && CurrentUser.Uid != "local_default_user";
    public bool IsNotLoggedIn => !IsLoggedIn;

    public IAsyncRelayCommand SyncNowCommand { get; }
    public IAsyncRelayCommand SignOutCommand { get; }
    public IAsyncRelayCommand ClearCloudCommand { get; }
    public IAsyncRelayCommand SignInGoogleCommand { get; }

    public SettingsViewModel(ILocalDataStore store, ISyncService syncService, IAuthService authService, ICloudSyncCoordinator syncCoordinator)
    {
        _store = store;
        _syncService = syncService;
        _authService = authService;
        _syncCoordinator = syncCoordinator;

        SyncNowCommand = new AsyncRelayCommand(PerformSyncAsync);
        SignOutCommand = new AsyncRelayCommand(SignOutAsync);
        ClearCloudCommand = new AsyncRelayCommand(ClearCloudDataAsync);
        SignInGoogleCommand = new AsyncRelayCommand(SignInGoogleAsync);

        _syncCoordinator.StateChanged += (s, state) => UpdateSyncStatus();
        _syncService.SyncStatusChanged += (s, status) => UpdateSyncStatus();
        _authService.AuthStateChanged += (s, user) =>
        {
            CurrentUser = user;
            OnPropertyChanged(nameof(IsLoggedIn));
            OnPropertyChanged(nameof(IsNotLoggedIn));
            OnPropertyChanged(nameof(ConnectedAccountEmail));
            UpdateSyncStatus();
        };
    }

    public async Task InitializeAsync()
    {
        AvailableCurrencies.Clear();
        foreach (var c in CurrencyConfig.SupportedCurrencies)
        {
            AvailableCurrencies.Add(c);
        }

        AvailableLanguages.Clear();
        foreach (var lang in LocalizationService.Current.SupportedLanguages)
        {
            AvailableLanguages.Add(lang);
        }

        AvailableSyncIntervals.Clear();
        foreach (var opt in SyncIntervalOption.DefaultOptions)
        {
            AvailableSyncIntervals.Add(opt);
        }
        _selectedSyncInterval = AvailableSyncIntervals.FirstOrDefault(o => o.Minutes == _syncCoordinator.SyncIntervalMinutes)
                                ?? AvailableSyncIntervals.FirstOrDefault(o => o.Minutes == 60);
        OnPropertyChanged(nameof(SelectedSyncInterval));
        OnPropertyChanged(nameof(IsAutoSyncEnabled));
        OnPropertyChanged(nameof(IsSyncOnStartupEnabled));
        OnPropertyChanged(nameof(ConnectedAccountEmail));

        var settings = await _store.GetSettingsAsync();
        SelectedCurrency = AvailableCurrencies.FirstOrDefault(c => c.Code == settings.BaseCurrencyCode) ?? AvailableCurrencies.FirstOrDefault(c => c.Code == "EUR");
        SelectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code.Equals(settings.SelectedLanguage, StringComparison.OrdinalIgnoreCase))
                           ?? AvailableLanguages.FirstOrDefault(l => l.Code == "SYSTEM");
        SelectedTheme = settings.DarkMode ?? "System";
        HideBalances = settings.HideBalances;

        CurrentUser = _authService.CurrentUser;
        UpdateSyncStatus();
    }

    private void UpdateSyncStatus()
    {
        var loc = LocalizationService.Current;
        SyncStatusText = _syncCoordinator.State switch
        {
            GlobalSyncState.Syncing => loc.GetString("Sync_Status_Syncing", "Syncing..."),
            GlobalSyncState.Success => loc.GetString("Sync_Status_Synchronized", "Synchronized"),
            GlobalSyncState.Error => loc.Format("Sync_Status_Error", _syncCoordinator.LastError ?? string.Empty),
            GlobalSyncState.Offline => loc.GetString("Sync_Status_Offline", "Offline"),
            GlobalSyncState.AuthenticationRequired => loc.GetString("Sync_Status_AuthRequired", "Sign In Required"),
            _ => loc.GetString("Sync_Status_Idle", "Idle")
        };

        if (_syncCoordinator.LastSyncTimestamp > 0)
        {
            var syncDto = DateTimeOffset.FromUnixTimeMilliseconds(_syncCoordinator.LastSyncTimestamp).LocalDateTime;
            if (syncDto.Date == DateTime.Today)
            {
                LastSyncText = $"Today, {syncDto:HH:mm}";
            }
            else
            {
                LastSyncText = syncDto.ToString("MMM dd, yyyy HH:mm", CultureInfo.CurrentCulture);
            }
        }
        else
        {
            LastSyncText = loc.GetString("Sync_LastSync_Never", "Never");
        }

        PendingChanges = _syncCoordinator.PendingChangesCount;
        OnPropertyChanged(nameof(IsAutoSyncEnabled));
        OnPropertyChanged(nameof(IsSyncOnStartupEnabled));
        OnPropertyChanged(nameof(ConnectedAccountEmail));
    }

    public async Task PerformSyncAsync()
    {
        var loc = LocalizationService.Current;
        IsBusy = true;
        BusyMessage = loc.GetString("Busy_Syncing");
        try
        {
            var res = await _syncCoordinator.SyncAsync(SyncTrigger.Manual);
            UpdateSyncStatus();
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    private async Task UpdateLanguageAsync(string code)
    {
        var settings = await _store.GetSettingsAsync();
        if (settings.SelectedLanguage == code) return;

        settings.SelectedLanguage = code;
        await _store.SaveSettingsAsync(settings);
        LocalizationService.Current.ApplyLanguage(code);

        // Refresh language list items in case display name of System Default changed
        var currentCode = code;
        AvailableLanguages.Clear();
        foreach (var lang in LocalizationService.Current.SupportedLanguages)
        {
            AvailableLanguages.Add(lang);
        }
        _selectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code.Equals(currentCode, StringComparison.OrdinalIgnoreCase))
                            ?? AvailableLanguages.FirstOrDefault(l => l.Code == "SYSTEM");
        OnPropertyChanged(nameof(SelectedLanguage));
        UpdateSyncStatus();
    }

    private async Task UpdateBaseCurrencyAsync(string code)
    {
        var settings = await _store.GetSettingsAsync();
        if (settings.BaseCurrencyCode == code) return;

        settings.BaseCurrencyCode = code;
        await _store.SaveSettingsAsync(settings);
    }

    private async Task UpdateThemeAsync(string theme)
    {
        var settings = await _store.GetSettingsAsync();
        settings.DarkMode = theme;
        await _store.SaveSettingsAsync(settings);

        if (App.RootWindow?.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
    }

    private async Task UpdateHideBalancesAsync(bool hide)
    {
        var settings = await _store.GetSettingsAsync();
        settings.HideBalances = hide;
        await _store.SaveSettingsAsync(settings);
    }

    public async Task SignInGoogleAsync()
    {
        var loc = LocalizationService.Current;
        IsBusy = true;
        BusyMessage = loc.GetString("Busy_SigningInGoogle");
        try
        {
            await _authService.SignInWithGoogleAsync();
            CurrentUser = _authService.CurrentUser;
            OnPropertyChanged(nameof(IsLoggedIn));
            OnPropertyChanged(nameof(IsNotLoggedIn));
            UpdateSyncStatus();
            await PerformSyncAsync();
        }
        catch (OperationCanceledException)
        {
            // Cancelled by user in browser
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Google Sign-In failed: {ex}");
            throw;
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    public async Task SignInGoogleAccountAsync(string email, string? displayName = null)
    {
        var loc = LocalizationService.Current;
        IsBusy = true;
        BusyMessage = loc.GetString("Busy_ConnectingGoogle");
        try
        {
            await _authService.SignInWithGoogleAccountAsync(email, displayName);
            CurrentUser = _authService.CurrentUser;
            OnPropertyChanged(nameof(IsLoggedIn));
            OnPropertyChanged(nameof(IsNotLoggedIn));
            UpdateSyncStatus();
            await PerformSyncAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Google Sign-In failed: {ex}");
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    public async Task SignInGoogleTokenAsync(string idToken)
    {
        var loc = LocalizationService.Current;
        IsBusy = true;
        BusyMessage = loc.GetString("Busy_AuthenticatingToken");
        try
        {
            await _authService.SignInWithGoogleTokenAsync(idToken);
            await PerformSyncAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Google Token Sign-In failed: {ex}");
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    public async Task SignInEmailAsync(string email, string password)
    {
        var loc = LocalizationService.Current;
        IsBusy = true;
        BusyMessage = loc.GetString("Busy_SigningInEmail");
        try
        {
            await _authService.SignInWithEmailPasswordAsync(email, password);
            await PerformSyncAsync();
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    private async Task SignOutAsync()
    {
        await _authService.SignOutAsync();
        CurrentUser = null;
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(IsNotLoggedIn));
        UpdateSyncStatus();
    }

    private async Task ClearCloudDataAsync()
    {
        await _syncService.ClearCloudDataAsync();
        UpdateSyncStatus();
    }
}
