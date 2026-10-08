using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using FinPulse.Windows.Services;
using FinPulse.Windows.ViewModels;

namespace FinPulse.Windows.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.InitializeAsync();
    }

    private async void SignInGoogleButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.SignInGoogleAsync();
        }
        catch (OperationCanceledException)
        {
            // Cancelled in browser by user
        }
        catch (Exception ex)
        {
            var loc = LocalizationService.Current;
            var dialog = new ContentDialog
            {
                Title = loc.GetString("Settings_GoogleDialog_Title"),
                Content = new TextBlock
                {
                    Text = $"{loc.GetString("Sync_Feedback_Failed")}: {ex.Message}",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13
                },
                PrimaryButtonText = loc.GetString("Common_OK"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}
