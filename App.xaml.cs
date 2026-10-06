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

    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();
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

        // Apply saved language before window creation
        try
        {
            var localStore = Services.GetRequiredService<ILocalDataStore>();
            localStore.InitializeAsync().GetAwaiter().GetResult();
            var settings = localStore.GetSettingsAsync().GetAwaiter().GetResult();
            var loc = Services.GetRequiredService<ILocalizationService>();
            loc.ApplyLanguage(settings.SelectedLanguage);
        }
        catch { }

        RootWindow = new MainWindow(initialTag);
        RootWindow.Activate();

        _ = InitializeServicesAsync();
    }

    private static async System.Threading.Tasks.Task InitializeServicesAsync()
    {
        try
        {
            var authService = Services.GetRequiredService<IAuthService>();
            var localStore = Services.GetRequiredService<ILocalDataStore>();
            var syncCoordinator = Services.GetRequiredService<ICloudSyncCoordinator>();

            await authService.InitializeAsync();
            await localStore.InitializeAsync();
            await syncCoordinator.InitializeAsync();

            syncCoordinator.TriggerStartupSync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize services: {ex}");
        }
    }

    public static void NavigateTo(string tag)
    {
        RootWindow?.NavigateToTag(tag);
    }
}
