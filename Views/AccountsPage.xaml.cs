using System;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using FinPulse.Windows.Models;
using FinPulse.Windows.ViewModels;

namespace FinPulse.Windows.Views;

public sealed partial class AccountsPage : Page
{
    public AccountsViewModel ViewModel { get; }

    public AccountsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<AccountsViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAccountsAsync();
    }

    private async void AddAccountButton_Click(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox { Header = "Account Name", PlaceholderText = "e.g. Main Checking, Savings" };
        var typeBox = new ComboBox
        {
            Header = "Account Type",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedIndex = 1
        };
        typeBox.Items.Add("Cash");
        typeBox.Items.Add("Bank Account");
        typeBox.Items.Add("Credit Card");
        typeBox.Items.Add("Savings Account");
        typeBox.Items.Add("Investment Account");
        typeBox.Items.Add("Digital Wallet");

        var balanceBox = new TextBox { Header = "Starting Balance", PlaceholderText = "0.00" };
        var institutionBox = new TextBox { Header = "Financial Institution", PlaceholderText = "e.g. Chase, ING, Revolut" };

        var currencyBox = new ComboBox
        {
            Header = "Currency",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        foreach (var c in CurrencyConfig.SupportedCurrencies)
        {
            currencyBox.Items.Add($"{c.Code} - {c.DisplayName} ({c.Symbol})");
        }
        currencyBox.SelectedIndex = 1; // EUR

        var panel = new StackPanel { Spacing = 12, Width = 360 };
        panel.Children.Add(nameBox);
        panel.Children.Add(typeBox);
        panel.Children.Add(currencyBox);
        panel.Children.Add(balanceBox);
        panel.Children.Add(institutionBox);

        var dialog = new ContentDialog
        {
            Title = "Create New Account",
            Content = panel,
            PrimaryButtonText = "Create Account",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            string name = string.IsNullOrWhiteSpace(nameBox.Text) ? "New Account" : nameBox.Text.Trim();
            var type = typeBox.SelectedIndex switch
            {
                0 => AccountType.CASH,
                1 => AccountType.BANK,
                2 => AccountType.CREDIT_CARD,
                3 => AccountType.SAVINGS,
                4 => AccountType.INVESTMENT,
                _ => AccountType.WALLET
            };

            decimal.TryParse(balanceBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var bal);
            string cur = CurrencyConfig.SupportedCurrencies[Math.Max(0, currencyBox.SelectedIndex)].Code;

            await ViewModel.AddAccountAsync(name, type, bal, cur, institutionBox.Text.Trim());
        }
    }

    private async void TransferButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Accounts.Count < 2)
        {
            var alert = new ContentDialog
            {
                Title = "Transfer Unavailable",
                Content = "You need at least two accounts to execute a transfer.",
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            };
            await alert.ShowAsync();
            return;
        }

        var sourceBox = new ComboBox { Header = "From Account", HorizontalAlignment = HorizontalAlignment.Stretch };
        var destBox = new ComboBox { Header = "To Account", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var acc in ViewModel.Accounts)
        {
            sourceBox.Items.Add($"{acc.Name} ({acc.FormattedBalance})");
            destBox.Items.Add($"{acc.Name} ({acc.FormattedBalance})");
        }
        sourceBox.SelectedIndex = 0;
        destBox.SelectedIndex = 1;

        var amountBox = new TextBox { Header = "Transfer Amount", PlaceholderText = "0.00" };
        var noteBox = new TextBox { Header = "Transfer Note", PlaceholderText = "Optional reference note" };

        var panel = new StackPanel { Spacing = 12, Width = 360 };
        panel.Children.Add(sourceBox);
        panel.Children.Add(destBox);
        panel.Children.Add(amountBox);
        panel.Children.Add(noteBox);

        var dialog = new ContentDialog
        {
            Title = "Transfer Between Accounts",
            Content = panel,
            PrimaryButtonText = "Execute Transfer",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            int sIdx = sourceBox.SelectedIndex;
            int dIdx = destBox.SelectedIndex;
            if (sIdx >= 0 && dIdx >= 0 && sIdx != dIdx)
            {
                decimal.TryParse(amountBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var amt);
                if (amt > 0)
                {
                    await ViewModel.TransferFundsAsync(ViewModel.Accounts[sIdx].Id, ViewModel.Accounts[dIdx].Id, amt, noteBox.Text);
                }
            }
        }
    }

    private async void DeleteAccount_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedAccount == null) return;

        var dialog = new ContentDialog
        {
            Title = "Delete Account?",
            Content = $"Are you sure you want to delete '{ViewModel.SelectedAccount.Name}'? This will remove its historical balance record.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.DeleteAccountCommand.ExecuteAsync(null);
        }
    }
}
