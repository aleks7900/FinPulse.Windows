using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using FinPulse.Windows.ViewModels;

namespace FinPulse.Windows.Views;

public sealed partial class OverviewPage : Page
{
    public OverviewViewModel ViewModel { get; }

    public OverviewPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<OverviewViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadDataAsync();
    }

    private async void QuickAddButton_Click(object sender, RoutedEventArgs e)
    {
        var quickAddDialog = new QuickAddDialog();
        quickAddDialog.XamlRoot = XamlRoot;
        await quickAddDialog.ShowAsync();
        await ViewModel.LoadDataAsync();
    }

    private void ViewAllTransactions_Click(object sender, RoutedEventArgs e)
    {
        App.NavigateTo("transactions");
    }

    private void ManageAccounts_Click(object sender, RoutedEventArgs e)
    {
        App.NavigateTo("accounts");
    }

    private void ViewBudgets_Click(object sender, RoutedEventArgs e)
    {
        App.NavigateTo("budgets");
    }

    private void ViewRecurring_Click(object sender, RoutedEventArgs e)
    {
        App.NavigateTo("recurring");
    }
}
