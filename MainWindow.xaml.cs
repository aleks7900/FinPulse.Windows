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
    private readonly ShellViewModel _viewModel;

    public MainWindow(string? initialTag = null)
    {
        InitializeComponent();

        _viewModel = App.Services.GetRequiredService<ShellViewModel>();

        var loc = App.Services.GetService<ILocalizationService>() ?? LocalizationService.Current;
        Title = loc.GetString("App_Title", "FinPulse Companion");

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

        // Set Desktop Window Size (1240 x 820)
        AppWindow.Resize(new SizeInt32(1240, 820));

        // Initial navigation
        NavigateToTag(!string.IsNullOrWhiteSpace(initialTag) ? initialTag : "overview");
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
        var sync = App.Services.GetRequiredService<ISyncService>();
        await sync.PerformFullSyncAsync();
    }

    private void Settings_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        NavigateToTag("settings");
    }
}
