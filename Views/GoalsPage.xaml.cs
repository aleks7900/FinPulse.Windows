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

public sealed partial class GoalsPage : Page
{
    public GoalsViewModel ViewModel { get; }

    public GoalsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<GoalsViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadGoalsAsync();
    }

    private async void AddGoalButton_Click(object sender, RoutedEventArgs e)
    {
        var titleBox = new TextBox { Header = "Goal Title", PlaceholderText = "e.g. New Laptop, Vacation, Emergency Buffer" };
        var targetBox = new TextBox { Header = "Target Amount", PlaceholderText = "0.00" };
        var datePicker = new DatePicker { Header = "Target Deadline Date", Date = DateTimeOffset.Now.AddMonths(6) };

        var accPicker = new ComboBox
        {
            Header = "Linked Account (Optional)",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = ViewModel.Accounts,
            DisplayMemberPath = "Name"
        };

        var panel = new StackPanel { Spacing = 12, Width = 360 };
        panel.Children.Add(titleBox);
        panel.Children.Add(targetBox);
        panel.Children.Add(datePicker);
        panel.Children.Add(accPicker);

        var dialog = new ContentDialog
        {
            Title = "Create Financial Goal",
            Content = panel,
            PrimaryButtonText = "Create Goal",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            decimal.TryParse(targetBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var target);
            string title = string.IsNullOrWhiteSpace(titleBox.Text) ? "Savings Goal" : titleBox.Text.Trim();
            string? linkedId = (accPicker.SelectedItem as Account)?.Id;

            if (target > 0)
            {
                await ViewModel.AddGoalAsync(title, target, datePicker.Date, linkedId);
            }
        }
    }

    private async void DepositButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string goalId)
        {
            var goal = ViewModel.Goals.FirstOrDefault(g => g.Id == goalId);
            if (goal == null) return;

            var amountBox = new TextBox { Header = "Deposit Amount", PlaceholderText = "0.00" };
            var panel = new StackPanel { Spacing = 8, Width = 320 };
            panel.Children.Add(new TextBlock { Text = $"Add funds toward '{goal.Title}'", FontSize = 13, Foreground = Application.Current.Resources["TextFillColorSecondaryBrush"] as Microsoft.UI.Xaml.Media.Brush });
            panel.Children.Add(amountBox);

            var dialog = new ContentDialog
            {
                Title = "Contribute to Goal",
                Content = panel,
                PrimaryButtonText = "Deposit Funds",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            var res = await dialog.ShowAsync();
            if (res == ContentDialogResult.Primary)
            {
                if (decimal.TryParse(amountBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var amt) && amt > 0)
                {
                    await ViewModel.ContributeToGoalAsync(goalId, amt);
                }
            }
        }
    }

    private async void DeleteGoal_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string goalId)
        {
            var dialog = new ContentDialog
            {
                Title = "Delete Goal?",
                Content = "Are you sure you want to delete this savings goal?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            var res = await dialog.ShowAsync();
            if (res == ContentDialogResult.Primary)
            {
                await ViewModel.DeleteGoalAsync(goalId);
            }
        }
    }
}
