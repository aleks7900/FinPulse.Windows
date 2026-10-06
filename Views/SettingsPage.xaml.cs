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

    private async void SignInEmailButton_Click(object sender, RoutedEventArgs e)
    {
        var loc = LocalizationService.Current;
        var emailBox = new TextBox
        {
            Header = loc.GetString("Settings_SignInDialog_EmailHeader"),
            PlaceholderText = "alex@example.com"
        };
        var passwordBox = new PasswordBox
        {
            Header = loc.GetString("Settings_SignInDialog_PasswordHeader")
        };

        var panel = new StackPanel { Spacing = 12, Width = 340 };
        panel.Children.Add(emailBox);
        panel.Children.Add(passwordBox);

        var dialog = new ContentDialog
        {
            Title = loc.GetString("Settings_SignInDialog_Title"),
            Content = panel,
            PrimaryButtonText = loc.GetString("Settings_SignInDialog_Button"),
            CloseButtonText = loc.GetString("Common_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(emailBox.Text))
        {
            await ViewModel.SignInEmailAsync(emailBox.Text.Trim(), passwordBox.Password);
        }
    }

    private async void SignInGoogleButton_Click(object sender, RoutedEventArgs e)
    {
        var loc = LocalizationService.Current;
        var googleEmailBox = new TextBox
        {
            Header = loc.GetString("Settings_GoogleDialog_EmailHeader"),
            PlaceholderText = "alex@gmail.com",
            Text = "alex@gmail.com"
        };
        var displayNameBox = new TextBox
        {
            Header = loc.GetString("Settings_GoogleDialog_NameHeader"),
            PlaceholderText = "Alex"
        };

        var browserAuthButton = new Button
        {
            Content = loc.GetString("Settings_GoogleDialog_BrowserButton"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 4)
        };

        var panel = new StackPanel { Spacing = 14, Width = 360 };

        panel.Children.Add(new TextBlock
        {
            Text = loc.GetString("Settings_GoogleDialog_Prompt"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13
        });

        panel.Children.Add(browserAuthButton);
        panel.Children.Add(googleEmailBox);
        panel.Children.Add(displayNameBox);

        var dialog = new ContentDialog
        {
            Title = loc.GetString("Settings_GoogleDialog_Title"),
            Content = panel,
            PrimaryButtonText = loc.GetString("Settings_GoogleDialog_ConnectButton"),
            CloseButtonText = loc.GetString("Common_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        browserAuthButton.Click += async (s, args) =>
        {
            dialog.Hide();
            await ViewModel.SignInGoogleAsync();
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(googleEmailBox.Text))
        {
            await ViewModel.SignInGoogleAccountAsync(googleEmailBox.Text.Trim(), displayNameBox.Text.Trim());
        }
    }
}
