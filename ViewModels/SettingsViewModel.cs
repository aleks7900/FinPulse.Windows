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

    public ObservableCollection<CurrencyMetadata> AvailableCurrencies { get; } = new();
    public ObservableCollection<LanguageOption> AvailableLanguages { get; } = new();

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

    public SettingsViewModel(ILocalDataStore store, ISyncService syncService, IAuthService authService)
    {
        _store = store;
        _syncService = syncService;
        _authService = authService;

        SyncNowCommand = new AsyncRelayCommand(PerformSyncAsync);
        SignOutCommand = new AsyncRelayCommand(SignOutAsync);
        ClearCloudCommand = new AsyncRelayCommand(ClearCloudDataAsync);
        SignInGoogleCommand = new AsyncRelayCommand(SignInGoogleAsync);

        _syncService.SyncStatusChanged += (s, status) => UpdateSyncStatus();
        _authService.AuthStateChanged += (s, user) =>
        {
            CurrentUser = user;
            OnPropertyChanged(nameof(IsLoggedIn));
            OnPropertyChanged(nameof(IsNotLoggedIn));
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
        SyncStatusText = _syncService.CurrentStatus switch
        {
            SyncStatus.SYNCING => loc.GetString("Sync_Status_Syncing"),
            SyncStatus.SUCCESS => loc.GetString("Sync_Status_Synchronized"),
            SyncStatus.ERROR => loc.Format("Sync_Status_Error", _syncService.LastErrorMessage ?? string.Empty),
            _ => loc.GetString("Sync_Status_Idle")
        };

        LastSyncText = _syncService.LastSyncTimestamp > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(_syncService.LastSyncTimestamp).LocalDateTime.ToString("MMM dd, yyyy HH:mm:ss", CultureInfo.CurrentCulture)
            : loc.GetString("Sync_LastSync_Never");

        PendingChanges = _syncService.PendingChangesCount;
    }

    public async Task PerformSyncAsync()
    {
        var loc = LocalizationService.Current;
        IsBusy = true;
        BusyMessage = loc.GetString("Busy_Syncing");
        try
        {
            var res = await _syncService.PerformFullSyncAsync();
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

    public async Task SignInGoogleAccountAsync(string email, string? displayName = null)
    {
        var loc = LocalizationService.Current;
        IsBusy = true;
        BusyMessage = loc.GetString("Busy_ConnectingGoogle");
        try
        {
            await _authService.SignInWithGoogleAccountAsync(email, displayName);
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
