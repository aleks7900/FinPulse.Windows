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
        var nameBox = new TextBox 
        { 
            Header = FinPulse.Windows.Services.LocalizationService.Current.GetString("Accounts_Dialog_NameHeader"), 
            PlaceholderText = FinPulse.Windows.Services.LocalizationService.Current.GetString("Accounts_Dialog_NamePlaceholder") 
        };
        var typeBox = new ComboBox
        {
            Header = FinPulse.Windows.Services.LocalizationService.Current.GetString("Accounts_Dialog_TypeHeader"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedIndex = 1
        };
        typeBox.Items.Add(FinPulse.Windows.Services.LocalizationService.Current.GetString("AccountType_Cash"));
        typeBox.Items.Add(FinPulse.Windows.Services.LocalizationService.Current.GetString("AccountType_Bank"));
        typeBox.Items.Add(FinPulse.Windows.Services.LocalizationService.Current.GetString("AccountType_CreditCard"));
        typeBox.Items.Add(FinPulse.Windows.Services.LocalizationService.Current.GetString("AccountType_Savings"));
        typeBox.Items.Add(FinPulse.Windows.Services.LocalizationService.Current.GetString("AccountType_Investment"));
        typeBox.Items.Add(FinPulse.Windows.Services.LocalizationService.Current.GetString("AccountType_Wallet"));

        var balanceBox = new TextBox 
        { 
            Header = FinPulse.Windows.Services.LocalizationService.Current.GetString("Accounts_Dialog_StartingBalanceHeader"), 
            PlaceholderText = FinPulse.Windows.Services.LocalizationService.Current.GetString("Common_AmountPlaceholder") 
        };
        var institutionBox = new TextBox 
        { 
            Header = FinPulse.Windows.Services.LocalizationService.Current.GetString("Accounts_Dialog_InstitutionHeader"), 
            PlaceholderText = FinPulse.Windows.Services.LocalizationService.Current.GetString("Accounts_Dialog_InstitutionPlaceholder") 
        };

        var currencyBox = new ComboBox
        {
            Header = FinPulse.Windows.Services.LocalizationService.Current.GetString("Common_CurrencyHeader"),
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
            Title = FinPulse.Windows.Services.LocalizationService.Current.GetString("Accounts_Dialog_CreateTitle"),
            Content = panel,
            PrimaryButtonText = FinPulse.Windows.Services.LocalizationService.Current.GetString("Accounts_Dialog_CreateButton"),
            CloseButtonText = FinPulse.Windows.Services.LocalizationService.Current.GetString("Common_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            string defaultName = FinPulse.Windows.Services.LocalizationService.Current.GetString("Accounts_Dialog_DefaultName");
            string name = string.IsNullOrWhiteSpace(nameBox.Text) ? defaultName : nameBox.Text.Trim();
            var type = typeBox.SelectedIndex switch
            {
                0 => AccountType.CASH,
                1 => AccountType.BANK,
                2 => AccountType.CREDIT_CARD,
                3 => AccountType.SAVINGS,
                4 => AccountType.INVESTMENT,
                _ => AccountType.WALLET
            };

            if (!decimal.TryParse(balanceBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var bal))
            {
                decimal.TryParse(balanceBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out bal);
            }
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
                Title = FinPulse.Windows.Services.LocalizationService.Current.GetString("Accounts_Transfer_UnavailableTitle"),
                Content = FinPulse.Windows.Services.LocalizationService.Current.GetString("Accounts_Transfer_UnavailableContent"),
                CloseButtonText = FinPulse.Windows.Services.LocalizationService.Current.GetString("Common_OK"),
                XamlRoot = XamlRoot
            };
            await alert.ShowAsync();
            return;
        }

        var sourceBox = new ComboBox 
        { 
            Header = FinPulse.Windows.Services.LocalizationService.Current.GetString("Accounts_Transfer_FromAccount"), 
            HorizontalAlignment = HorizontalAlignment.Stretch 
        };
        var destBox = new ComboBox 
        { 
            Header = FinPulse.Windows.Services.LocalizationService.Current.GetString("Accounts_Transfer_ToAccount"), 
            HorizontalAlignment = HorizontalAlignment.Stretch 
        };
        foreach (var acc in ViewModel.Accounts)
        {
            sourceBox.Items.Add($"{acc.Name} ({acc.FormattedBalance})");
            destBox.Items.Add($"{acc.Name} ({acc.FormattedBalance})");
        }
        sourceBox.SelectedIndex = 0;
        destBox.SelectedIndex = 1;

        var amountBox = new TextBox 
        { 
            Header = FinPulse.Windows.Services.LocalizationService.Current.GetString("Accounts_Transfer_AmountHeader"), 
            PlaceholderText = FinPulse.Windows.Services.LocalizationService.Current.GetString("Common_AmountPlaceholder") 
        };
        var noteBox = new TextBox 
        { 
            Header = FinPulse.Windows.Services.LocalizationService.Current.GetString("Accounts_Transfer_NoteHeader"), 
            PlaceholderText = FinPulse.Windows.Services.LocalizationService.Current.GetString("Accounts_Transfer_NotePlaceholder") 
        };

        var panel = new StackPanel { Spacing = 12, Width = 360 };
        panel.Children.Add(sourceBox);
        panel.Children.Add(destBox);
        panel.Children.Add(amountBox);
        panel.Children.Add(noteBox);

        var dialog = new ContentDialog
        {
            Title = FinPulse.Windows.Services.LocalizationService.Current.GetString("Accounts_Transfer_DialogTitle"),
            Content = panel,
            PrimaryButtonText = FinPulse.Windows.Services.LocalizationService.Current.GetString("Accounts_Transfer_ExecuteButton"),
            CloseButtonText = FinPulse.Windows.Services.LocalizationService.Current.GetString("Common_Cancel"),
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
                if (!decimal.TryParse(amountBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var amt))
                {
                    decimal.TryParse(amountBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out amt);
                }
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
            Title = FinPulse.Windows.Services.LocalizationService.Current.GetString("Accounts_Delete_DialogTitle"),
            Content = FinPulse.Windows.Services.LocalizationService.Current.Format("Accounts_Delete_DialogContent", ViewModel.SelectedAccount.Name),
            PrimaryButtonText = FinPulse.Windows.Services.LocalizationService.Current.GetString("Common_Delete"),
            CloseButtonText = FinPulse.Windows.Services.LocalizationService.Current.GetString("Common_Cancel"),
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
