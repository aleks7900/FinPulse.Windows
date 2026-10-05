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

    private async void SignInDemoButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.SignInDemoAsync();
    }
}
