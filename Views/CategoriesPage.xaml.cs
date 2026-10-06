using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using FinPulse.Windows.Models;
using FinPulse.Windows.ViewModels;

namespace FinPulse.Windows.Views;

public sealed partial class CategoriesPage : Page
{
    public CategoriesViewModel ViewModel { get; }

    public CategoriesPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<CategoriesViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadCategoriesAsync();
    }

    private async void AddCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox 
        { 
            Header = FinPulse.Windows.Services.LocalizationService.Current.GetString("Categories_Dialog_NameHeader"), 
            PlaceholderText = FinPulse.Windows.Services.LocalizationService.Current.GetString("Categories_Dialog_NamePlaceholder") 
        };
        var typeBox = new ComboBox
        {
            Header = FinPulse.Windows.Services.LocalizationService.Current.GetString("Categories_Dialog_TypeHeader"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedIndex = 0
        };
        typeBox.Items.Add(FinPulse.Windows.Services.LocalizationService.Current.GetString("Common_Expense"));
        typeBox.Items.Add(FinPulse.Windows.Services.LocalizationService.Current.GetString("Common_Income"));

        var panel = new StackPanel { Spacing = 12, Width = 340 };
        panel.Children.Add(nameBox);
        panel.Children.Add(typeBox);

        var dialog = new ContentDialog
        {
            Title = FinPulse.Windows.Services.LocalizationService.Current.GetString("Categories_Dialog_AddTitle"),
            Content = panel,
            PrimaryButtonText = FinPulse.Windows.Services.LocalizationService.Current.GetString("Categories_Dialog_SaveButton"),
            CloseButtonText = FinPulse.Windows.Services.LocalizationService.Current.GetString("Common_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(nameBox.Text))
        {
            var type = typeBox.SelectedIndex == 1 ? CategoryType.INCOME : CategoryType.EXPENSE;
            long color = type == CategoryType.INCOME ? 0xFF2E7D32 : 0xFF2196F3;
            await ViewModel.AddCategoryAsync(nameBox.Text.Trim(), type, "category", color);
        }
    }
}
