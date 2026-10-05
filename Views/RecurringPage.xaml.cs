using System;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using FinPulse.Windows.Models;
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
        var titleBox = new TextBox { Header = "Subscription / Bill Title", PlaceholderText = "e.g. Internet, Spotify, Gym" };
        var amountBox = new TextBox { Header = "Billing Amount", PlaceholderText = "0.00" };

        var freqBox = new ComboBox
        {
            Header = "Billing Frequency",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedIndex = 3 // Monthly
        };
        freqBox.Items.Add("Daily");
        freqBox.Items.Add("Weekly");
        freqBox.Items.Add("Every 2 Weeks");
        freqBox.Items.Add("Monthly");
        freqBox.Items.Add("Quarterly");
        freqBox.Items.Add("Yearly");

        var accBox = new ComboBox
        {
            Header = "Payment Account",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = ViewModel.Accounts,
            DisplayMemberPath = "Name"
        };
        accBox.SelectedIndex = 0;

        var catBox = new ComboBox
        {
            Header = "Category",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = ViewModel.Categories,
            DisplayMemberPath = "Name"
        };
        catBox.SelectedIndex = 0;

        var duePicker = new DatePicker { Header = "First / Next Due Date", Date = DateTimeOffset.Now.AddDays(7) };

        var panel = new StackPanel { Spacing = 12, Width = 360 };
        panel.Children.Add(titleBox);
        panel.Children.Add(amountBox);
        panel.Children.Add(freqBox);
        panel.Children.Add(accBox);
        panel.Children.Add(catBox);
        panel.Children.Add(duePicker);

        var dialog = new ContentDialog
        {
            Title = "Add Recurring Obligation",
            Content = panel,
            PrimaryButtonText = "Save Rule",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && accBox.SelectedItem is Account acc && catBox.SelectedItem is Category cat)
        {
            decimal.TryParse(amountBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var amt);
            string title = string.IsNullOrWhiteSpace(titleBox.Text) ? "Recurring Obligation" : titleBox.Text.Trim();

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
            var dialog = new ContentDialog
            {
                Title = "Delete Recurring Payment?",
                Content = "Are you sure you want to delete this recurring schedule?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
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
