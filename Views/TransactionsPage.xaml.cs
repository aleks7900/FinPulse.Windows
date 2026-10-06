using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using FinPulse.Windows.Services;
using FinPulse.Windows.ViewModels;

namespace FinPulse.Windows.Views;

public sealed partial class TransactionsPage : Page
{
    public TransactionsViewModel ViewModel { get; }

    public TransactionsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<TransactionsViewModel>();
        DataContext = ViewModel;

        ToolTipService.SetToolTip(ResetFiltersBtn, LocalizationService.Current.GetString("Transactions_ResetFilters_Tooltip"));
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadDataAsync();
    }

    private async void AddTransactionButton_Click(object sender, RoutedEventArgs e)
    {
        var quickAdd = new QuickAddDialog { XamlRoot = XamlRoot };
        await quickAdd.ShowAsync();
        await ViewModel.LoadDataAsync();
    }

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SearchQuery = string.Empty;
        ViewModel.SelectedTypeFilter = "All";
        ViewModel.SelectedAccountFilter = null;
        ViewModel.SelectedCategoryFilter = null;
        ViewModel.SelectedDateFilter = "All Time";
    }

    private async void DeleteRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string txId)
        {
            var loc = LocalizationService.Current;
            var dialog = new ContentDialog
            {
                Title = loc.GetString("Transactions_Delete_DialogTitle"),
                Content = loc.GetString("Transactions_Delete_DialogContent"),
                PrimaryButtonText = loc.GetString("Common_Delete"),
                CloseButtonText = loc.GetString("Common_Cancel"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            var res = await dialog.ShowAsync();
            if (res == ContentDialogResult.Primary)
            {
                var store = App.Services.GetRequiredService<Services.ILocalDataStore>();
                await store.DeleteTransactionAsync(txId);
                await ViewModel.LoadDataAsync();
            }
        }
    }
}
