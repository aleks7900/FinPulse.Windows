using System;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using FinPulse.Windows.Models;
using FinPulse.Windows.Services;
using FinPulse.Windows.ViewModels;

namespace FinPulse.Windows.Views;

public sealed partial class RecurringPage : Page
{
    public RecurringViewModel ViewModel { get; }

    public RecurringPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<RecurringViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadDataAsync();
    }

    private async void AddRuleButton_Click(object sender, RoutedEventArgs e)
    {
        var loc = LocalizationService.Current;
        var titleBox = new TextBox
        {
            Header = loc.GetString("Recurring_Dialog_TitleHeader"),
            PlaceholderText = loc.GetString("Recurring_Dialog_TitlePlaceholder")
        };
        var amountBox = new TextBox
        {
            Header = loc.GetString("Recurring_Dialog_AmountHeader"),
            PlaceholderText = loc.GetString("Common_AmountPlaceholder")
        };

        var freqBox = new ComboBox
        {
            Header = loc.GetString("Recurring_Dialog_FrequencyHeader"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedIndex = 3 // Monthly
        };
        freqBox.Items.Add(loc.GetString("Frequency_Daily"));
        freqBox.Items.Add(loc.GetString("Frequency_Weekly"));
        freqBox.Items.Add(loc.GetString("Frequency_BiWeekly"));
        freqBox.Items.Add(loc.GetString("Frequency_Monthly"));
        freqBox.Items.Add(loc.GetString("Frequency_Quarterly"));
        freqBox.Items.Add(loc.GetString("Frequency_Yearly"));

        var accBox = new ComboBox
        {
            Header = loc.GetString("Recurring_Dialog_AccountHeader"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = ViewModel.Accounts,
            DisplayMemberPath = "Name"
        };
        accBox.SelectedIndex = 0;

        var catBox = new ComboBox
        {
            Header = loc.GetString("Recurring_Dialog_CategoryHeader"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = ViewModel.Categories,
            DisplayMemberPath = "Name"
        };
        catBox.SelectedIndex = 0;

        var duePicker = new DatePicker
        {
            Header = loc.GetString("Recurring_Dialog_DueDateHeader"),
            Date = DateTimeOffset.Now.AddDays(7)
        };

        var panel = new StackPanel { Spacing = 12, Width = 360 };
        panel.Children.Add(titleBox);
        panel.Children.Add(amountBox);
        panel.Children.Add(freqBox);
        panel.Children.Add(accBox);
        panel.Children.Add(catBox);
        panel.Children.Add(duePicker);

        var dialog = new ContentDialog
        {
            Title = loc.GetString("Recurring_Dialog_AddTitle"),
            Content = panel,
            PrimaryButtonText = loc.GetString("Recurring_Dialog_SaveButton"),
            CloseButtonText = loc.GetString("Common_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && accBox.SelectedItem is Account acc && catBox.SelectedItem is Category cat)
        {
            if (!decimal.TryParse(amountBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var amt))
            {
                decimal.TryParse(amountBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out amt);
            }

            string defaultTitle = loc.GetString("Recurring_Dialog_DefaultTitle");
            string title = string.IsNullOrWhiteSpace(titleBox.Text) ? defaultTitle : titleBox.Text.Trim();

            var freq = freqBox.SelectedIndex switch
            {
                0 => PaymentFrequency.DAILY,
                1 => PaymentFrequency.WEEKLY,
                2 => PaymentFrequency.BI_WEEKLY,
                4 => PaymentFrequency.QUARTERLY,
                5 => PaymentFrequency.YEARLY,
                _ => PaymentFrequency.MONTHLY
            };

            if (amt > 0)
            {
                await ViewModel.AddRecurringRuleAsync(title, amt, freq, acc.Id, cat.Id, duePicker.Date, isSubscription: true);
            }
        }
    }

    private async void MarkPaidButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string ruleId)
        {
            var rule = ViewModel.RecurringRules.FirstOrDefault(r => r.Id == ruleId);
            if (rule != null)
            {
                await ViewModel.MarkAsPaidAsync(rule);
            }
        }
    }

    private async void DeleteRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string ruleId)
        {
            var loc = LocalizationService.Current;
            var dialog = new ContentDialog
            {
                Title = loc.GetString("Recurring_Delete_DialogTitle"),
                Content = loc.GetString("Recurring_Delete_DialogContent"),
                PrimaryButtonText = loc.GetString("Common_Delete"),
                CloseButtonText = loc.GetString("Common_Cancel"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            var res = await dialog.ShowAsync();
            if (res == ContentDialogResult.Primary)
            {
                await ViewModel.DeleteRuleAsync(ruleId);
            }
        }
    }
}
