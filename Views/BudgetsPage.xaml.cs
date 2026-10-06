using System;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using FinPulse.Windows.Models;
using FinPulse.Windows.ViewModels;

namespace FinPulse.Windows.Views;

public sealed partial class BudgetsPage : Page
{
    public BudgetsViewModel ViewModel { get; }

    public BudgetsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<BudgetsViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadBudgetsAsync();
    }

    private async void AddBudgetButton_Click(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox 
        { 
            Header = FinPulse.Windows.Services.LocalizationService.Current.GetString("Budgets_Dialog_NameHeader"), 
            PlaceholderText = FinPulse.Windows.Services.LocalizationService.Current.GetString("Budgets_Dialog_NamePlaceholder") 
        };
        var catBox = new ComboBox
        {
            Header = FinPulse.Windows.Services.LocalizationService.Current.GetString("Budgets_Dialog_CategoryHeader"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = ViewModel.AvailableCategories,
            DisplayMemberPath = "Name"
        };
        catBox.SelectedIndex = 0;

        var limitBox = new TextBox 
        { 
            Header = FinPulse.Windows.Services.LocalizationService.Current.GetString("Budgets_Dialog_LimitHeader"), 
            PlaceholderText = FinPulse.Windows.Services.LocalizationService.Current.GetString("Common_AmountPlaceholder") 
        };

        var panel = new StackPanel { Spacing = 12, Width = 360 };
        panel.Children.Add(nameBox);
        panel.Children.Add(catBox);
        panel.Children.Add(limitBox);

        var dialog = new ContentDialog
        {
            Title = FinPulse.Windows.Services.LocalizationService.Current.GetString("Budgets_Dialog_CreateTitle"),
            Content = panel,
            PrimaryButtonText = FinPulse.Windows.Services.LocalizationService.Current.GetString("Budgets_Dialog_CreateButton"),
            CloseButtonText = FinPulse.Windows.Services.LocalizationService.Current.GetString("Common_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && catBox.SelectedItem is Category selectedCat)
        {
            if (!decimal.TryParse(limitBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var limit))
            {
                decimal.TryParse(limitBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out limit);
            }
            string name = string.IsNullOrWhiteSpace(nameBox.Text) ? selectedCat.Name : nameBox.Text.Trim();
            if (limit > 0)
            {
                await ViewModel.AddBudgetAsync(name, selectedCat.Id, limit, BudgetPeriod.MONTHLY);
            }
        }
    }

    private async void DeleteBudget_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
        {
            var dialog = new ContentDialog
            {
                Title = FinPulse.Windows.Services.LocalizationService.Current.GetString("Budgets_Delete_DialogTitle"),
                Content = FinPulse.Windows.Services.LocalizationService.Current.GetString("Budgets_Delete_DialogContent"),
                PrimaryButtonText = FinPulse.Windows.Services.LocalizationService.Current.GetString("Common_Delete"),
                CloseButtonText = FinPulse.Windows.Services.LocalizationService.Current.GetString("Common_Cancel"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            var res = await dialog.ShowAsync();
            if (res == ContentDialogResult.Primary)
            {
                await ViewModel.DeleteBudgetAsync(id);
            }
        }
    }
}
