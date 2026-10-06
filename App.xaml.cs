using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using FinPulse.Windows.Repositories;
using FinPulse.Windows.Services;
using FinPulse.Windows.ViewModels;

namespace FinPulse.Windows;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static MainWindow? RootWindow { get; private set; }

    public static void LogDiagnostic(string msg)
    {
        try
        {
            string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FinPulseCompanion");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "app_debug.log"), $"[{DateTime.Now:O}] {msg}\n");
        }
        catch { }
    }

    public App()
    {
        LogDiagnostic("App constructor start");
        UnhandledException += (s, e) =>
        {
            LogDiagnostic($"[UnhandledException] {e.Message} | {e.Exception}");
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            LogDiagnostic($"[AppDomain Unhandled] {e.ExceptionObject}");
        };
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            LogDiagnostic($"[UnobservedTaskException] {e.Exception}");
        };

        InitializeComponent();
        Services = ConfigureServices();
        LogDiagnostic("App constructor finished");
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Core Services
        services.AddSingleton<HttpClient>();
        services.AddSingleton<ISecureCredentialStorage, WindowsSecureCredentialStorage>();
        services.AddSingleton<IGoogleOAuthHandler, GoogleOAuthHandler>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<IAuthService, FirebaseAuthService>();
        services.AddSingleton<IFirestoreClient, FirestoreRestClient>();
        services.AddSingleton<ILocalDataStore, LocalDataStore>();
        services.AddSingleton<ISyncService, CloudSyncService>();
        services.AddSingleton<ICloudSyncCoordinator, CloudSyncCoordinator>();
        services.AddSingleton<INotificationService, WindowsNotificationService>();

        // Repositories
        services.AddSingleton<IAccountRepository, AccountRepository>();
        services.AddSingleton<ITransactionRepository, TransactionRepository>();
        services.AddSingleton<ICategoryRepository, CategoryRepository>();
        services.AddSingleton<IBudgetRepository, BudgetRepository>();
        services.AddSingleton<IGoalRepository, GoalRepository>();
        services.AddSingleton<IRecurringRepository, RecurringRepository>();

        // ViewModels
        services.AddSingleton<ShellViewModel>();
        services.AddTransient<OverviewViewModel>();
        services.AddTransient<AccountsViewModel>();
        services.AddTransient<TransactionsViewModel>();
        services.AddTransient<BudgetsViewModel>();
        services.AddTransient<GoalsViewModel>();
        services.AddTransient<RecurringViewModel>();
        services.AddTransient<CategoriesViewModel>();
        services.AddTransient<AnalyticsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<QuickAddViewModel>();

        return services.BuildServiceProvider();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        string? initialTag = null;
        string[] cmdArgs = Environment.GetCommandLineArgs();
        for (int i = 0; i < cmdArgs.Length; i++)
        {
            if (cmdArgs[i] == "--page" && i + 1 < cmdArgs.Length)
            {
                initialTag = cmdArgs[i + 1];
                break;
            }
        }

        // Apply saved language before window creation without blocking async deadlock
        try
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string settingsPath = System.IO.Path.Combine(localAppData, "FinPulseCompanion", "store", "settings.json");
            if (System.IO.File.Exists(settingsPath))
            {
                string json = System.IO.File.ReadAllText(settingsPath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("selectedLanguage", out var langProp))
                {
                    string? lang = langProp.GetString();
                    if (!string.IsNullOrEmpty(lang))
                    {
                        var loc = Services.GetRequiredService<ILocalizationService>();
                        loc.ApplyLanguage(lang);
                    }
                }
            }
        }
        catch { }

        LogDiagnostic($"OnLaunched: creating MainWindow with tag={initialTag}");
        RootWindow = new MainWindow(initialTag);
        LogDiagnostic("OnLaunched: activating RootWindow");
        RootWindow.Activate();
        try { RootWindow.AppWindow.Show(); } catch { }
        LogDiagnostic($"OnLaunched: RootWindow activated. IsVisible={RootWindow.AppWindow?.IsVisible}");

        _ = InitializeServicesAsync();
    }

    private static async System.Threading.Tasks.Task InitializeServicesAsync()
    {
        try
        {
            LogDiagnostic("InitializeServicesAsync: starting");
            var authService = Services.GetRequiredService<IAuthService>();
            var localStore = Services.GetRequiredService<ILocalDataStore>();
            var syncCoordinator = Services.GetRequiredService<ICloudSyncCoordinator>();

            LogDiagnostic("InitializeServicesAsync: initializing authService");
            await authService.InitializeAsync();
            LogDiagnostic("InitializeServicesAsync: initializing localStore");
            await localStore.InitializeAsync();
            LogDiagnostic("InitializeServicesAsync: initializing syncCoordinator");
            await syncCoordinator.InitializeAsync();

            LogDiagnostic("InitializeServicesAsync: triggering startup sync");
            syncCoordinator.TriggerStartupSync();
            LogDiagnostic("InitializeServicesAsync: completed successfully");
        }
        catch (Exception ex)
        {
            LogDiagnostic($"InitializeServicesAsync: EXCEPTION: {ex}");
        }
    }

    public static void NavigateTo(string tag)
    {
        RootWindow?.NavigateToTag(tag);
    }
}
