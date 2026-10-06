using System;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using FinPulse.Windows.Models;
using FinPulse.Windows.Services;
using FinPulse.Windows.ViewModels;

namespace FinPulse.Windows.Views;

public sealed partial class QuickAddDialog : ContentDialog
{
    public QuickAddViewModel ViewModel { get; }

    public string AmountString
    {
        get => ViewModel.Amount > 0 ? ViewModel.Amount.ToString("0.00", CultureInfo.CurrentCulture) : string.Empty;
        set
        {
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out var amtLocal))
            {
                ViewModel.Amount = amtLocal;
            }
            else if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var amt))
            {
                ViewModel.Amount = amt;
            }
        }
    }

    public int SelectedTypeIndex
    {
        get => ViewModel.SelectedType switch
        {
            TransactionType.EXPENSE => 0,
            TransactionType.INCOME => 1,
            TransactionType.TRANSFER => 2,
            _ => 0
        };
        set
        {
            ViewModel.SelectedType = value switch
            {
                1 => TransactionType.INCOME,
                2 => TransactionType.TRANSFER,
                _ => TransactionType.EXPENSE
            };
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ViewModel.ErrorMessage);

    public QuickAddDialog()
    {
        InitializeComponent();
        Title = LocalizationService.Current.GetString("QuickAdd_Title.Title");
        PrimaryButtonText = LocalizationService.Current.GetString("QuickAdd_SaveButton.Content");
        CloseButtonText = LocalizationService.Current.GetString("Common_Cancel");

        ViewModel = App.Services.GetRequiredService<QuickAddViewModel>();
        DataContext = ViewModel;

        Loaded += async (s, e) =>
        {
            await ViewModel.InitializeAsync();
            AmountTextBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        };

        PrimaryButtonClick += async (sender, args) =>
        {
            var deferral = args.GetDeferral();
            bool success = await ViewModel.SaveTransactionAsync();
            if (!success)
            {
                args.Cancel = true;
            }
            deferral.Complete();
        };
    }

    private void AmountTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            // Trigger primary button click programmatically
        }
    }
}
