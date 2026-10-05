using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
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
        var emailBox = new TextBox { Header = "Email Address", PlaceholderText = "alex@example.com" };
        var passwordBox = new PasswordBox { Header = "Password" };

        var panel = new StackPanel { Spacing = 12, Width = 340 };
        panel.Children.Add(emailBox);
        panel.Children.Add(passwordBox);

        var dialog = new ContentDialog
        {
            Title = "Sign in to FinPulse",
            Content = panel,
            PrimaryButtonText = "Sign In",
            CloseButtonText = "Cancel",
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
        var googleEmailBox = new TextBox
        {
            Header = "Google Account Email",
            PlaceholderText = "alex@gmail.com",
            Text = "alex@gmail.com"
        };
        var displayNameBox = new TextBox
        {
            Header = "Display Name (Optional)",
            PlaceholderText = "Alex"
        };

        var browserAuthButton = new Button
        {
            Content = "Sign in via System Web Browser (OAuth 2.0)",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 4)
        };

        var panel = new StackPanel { Spacing = 14, Width = 360 };

        panel.Children.Add(new TextBlock
        {
            Text = "Connect your Google account to synchronize your financial accounts, transactions, and budgets with FinPulse on Android.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13
        });

        panel.Children.Add(browserAuthButton);
        panel.Children.Add(googleEmailBox);
        panel.Children.Add(displayNameBox);

        var dialog = new ContentDialog
        {
            Title = "Sign In with Google",
            Content = panel,
            PrimaryButtonText = "Connect Google Account",
            CloseButtonText = "Cancel",
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
