using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
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
            var dialog = new ContentDialog
            {
                Title = "Delete Transaction?",
                Content = "Are you sure you want to delete this transaction? Account balances will update automatically.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
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
