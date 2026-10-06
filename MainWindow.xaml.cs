using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using FinPulse.Windows.Services;
using FinPulse.Windows.ViewModels;
using FinPulse.Windows.Views;

namespace FinPulse.Windows;

public sealed partial class MainWindow : Window
{
    public ShellViewModel ViewModel { get; }

    public MainWindow(string? initialTag = null)
    {
        ViewModel = App.Services.GetRequiredService<ShellViewModel>();
        InitializeComponent();

        var loc = App.Services.GetService<ILocalizationService>() ?? LocalizationService.Current;
        loc.LanguageChanged += (s, e) =>
        {
            DispatcherQueue.TryEnqueue(RefreshLocalizedStrings);
        };

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

        // Set Desktop Window Size (1240 x 820)
        AppWindow.Resize(new SizeInt32(1240, 820));

        Activated += MainWindow_Activated;
        Closed += MainWindow_Closed;

        RefreshLocalizedStrings();

        // Initial navigation
        NavigateToTag(!string.IsNullOrWhiteSpace(initialTag) ? initialTag : "overview");
    }

    public void RefreshLocalizedStrings()
    {
        var loc = App.Services.GetService<ILocalizationService>() ?? LocalizationService.Current;
        Title = loc.GetString("App_Title", "FinPulse Companion");
        AppTitleBar.Title = loc.GetString("AppTitleBar.Title", "FinPulse Companion");
        AppTitleBar.Subtitle = loc.GetString("AppTitleBar.Subtitle", "Windows 11 Edition");

        foreach (var item in NavView.MenuItems.OfType<NavigationViewItem>())
        {
            if (item.Tag is string tag)
            {
                string key = tag switch
                {
                    "overview" => "Nav_Overview.Content",
                    "accounts" => "Nav_Accounts.Content",
                    "transactions" => "Nav_Transactions.Content",
                    "analytics" => "Nav_Analytics.Content",
                    "budgets" => "Nav_Budgets.Content",
                    "goals" => "Nav_Goals.Content",
                    "recurring" => "Nav_Recurring.Content",
                    "categories" => "Nav_Categories.Content",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(key))
                {
                    item.Content = loc.GetString(key, item.Content?.ToString());
                }
            }
        }

        if (NavView.SettingsItem is NavigationViewItem settingsItem)
        {
            settingsItem.Content = loc.GetString("Nav_Settings.Content", "Settings");
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        App.LogDiagnostic($"MainWindow_Closed triggered. Handled={args.Handled}");
        try
        {
            (App.Services.GetService<ICloudSyncCoordinator>() as IDisposable)?.Dispose();
            (App.Services.GetService<ISyncService>() as IDisposable)?.Dispose();
            (App.Services.GetService<ILocalDataStore>() as IDisposable)?.Dispose();
        }
        catch (Exception ex)
        {
            App.LogDiagnostic($"Exception in MainWindow_Closed: {ex}");
        }
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        App.LogDiagnostic($"MainWindow_Activated state={args.WindowActivationState}");
        if (args.WindowActivationState != WindowActivationState.Deactivated)
        {
            try
            {
                var coordinator = App.Services.GetService<ICloudSyncCoordinator>();
                coordinator?.TriggerForegroundSync();
            }
            catch (Exception ex)
            {
                App.LogDiagnostic($"Exception in TriggerForegroundSync: {ex}");
            }
        }
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void TitleBar_BackRequested(TitleBar sender, object args)
    {
        if (NavFrame.CanGoBack)
        {
            NavFrame.GoBack();
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavFrame.Navigate(typeof(SettingsPage));
        }
        else if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            NavigateToTag(tag);
        }
    }

    public void NavigateToTag(string tag)
    {
        Type pageType = tag switch
        {
            "overview" => typeof(OverviewPage),
            "accounts" => typeof(AccountsPage),
            "transactions" => typeof(TransactionsPage),
            "analytics" => typeof(AnalyticsPage),
            "budgets" => typeof(BudgetsPage),
            "goals" => typeof(GoalsPage),
            "recurring" => typeof(RecurringPage),
            "categories" => typeof(CategoriesPage),
            "settings" => typeof(SettingsPage),
            _ => typeof(OverviewPage)
        };

        if (NavFrame.CurrentSourcePageType != pageType)
        {
            NavFrame.Navigate(pageType);
        }

        // Synchronize selected navigation item
        if (tag == "settings")
        {
            NavView.SelectedItem = NavView.SettingsItem;
        }
        else
        {
            var targetItem = NavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => i.Tag?.ToString() == tag);
            if (targetItem != null)
            {
                NavView.SelectedItem = targetItem;
            }
        }
    }

    public async void OpenQuickAddDialog()
    {
        var quickAdd = new QuickAddDialog { XamlRoot = Content.XamlRoot };
        await quickAdd.ShowAsync();
    }

    private void QuickAdd_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        OpenQuickAddDialog();
    }

    private void Search_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        NavigateToTag("transactions");
    }

    private async void Refresh_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        var coordinator = App.Services.GetRequiredService<ICloudSyncCoordinator>();
        await coordinator.SyncAsync(SyncTrigger.Manual);
    }

    private void Settings_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        NavigateToTag("settings");
    }
}
