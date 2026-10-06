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
        var loc = LocalizationService.Current;
        var titleBox = new TextBox
        {
            Header = loc.GetString("Goals_Dialog_TitleHeader"),
            PlaceholderText = loc.GetString("Goals_Dialog_TitlePlaceholder")
        };
        var targetBox = new TextBox
        {
            Header = loc.GetString("Goals_Dialog_AmountHeader"),
            PlaceholderText = loc.GetString("Common_AmountPlaceholder")
        };
        var datePicker = new DatePicker
        {
            Header = loc.GetString("Goals_Dialog_DeadlineHeader"),
            Date = DateTimeOffset.Now.AddMonths(6)
        };

        var accPicker = new ComboBox
        {
            Header = loc.GetString("Goals_Dialog_LinkedAccountHeader"),
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
            Title = loc.GetString("Goals_Dialog_CreateTitle"),
            Content = panel,
            PrimaryButtonText = loc.GetString("Goals_Dialog_CreateButton"),
            CloseButtonText = loc.GetString("Common_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (!decimal.TryParse(targetBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var target))
            {
                decimal.TryParse(targetBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out target);
            }

            string defaultTitle = loc.GetString("Goals_Dialog_DefaultTitle");
            string title = string.IsNullOrWhiteSpace(titleBox.Text) ? defaultTitle : titleBox.Text.Trim();
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

            var loc = LocalizationService.Current;
            var amountBox = new TextBox
            {
                Header = loc.GetString("Goals_Deposit_AmountHeader"),
                PlaceholderText = loc.GetString("Common_AmountPlaceholder")
            };
            var panel = new StackPanel { Spacing = 8, Width = 320 };
            panel.Children.Add(new TextBlock
            {
                Text = loc.Format("Goals_Deposit_Prompt", goal.Title),
                FontSize = 13,
                Foreground = Application.Current.Resources["TextFillColorSecondaryBrush"] as Microsoft.UI.Xaml.Media.Brush
            });
            panel.Children.Add(amountBox);

            var dialog = new ContentDialog
            {
                Title = loc.GetString("Goals_Deposit_DialogTitle"),
                Content = panel,
                PrimaryButtonText = loc.GetString("Goals_Deposit_ExecuteButton"),
                CloseButtonText = loc.GetString("Common_Cancel"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            var res = await dialog.ShowAsync();
            if (res == ContentDialogResult.Primary)
            {
                if (decimal.TryParse(amountBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var amt) ||
                    decimal.TryParse(amountBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out amt))
                {
                    if (amt > 0)
                    {
                        await ViewModel.ContributeToGoalAsync(goalId, amt);
                    }
                }
            }
        }
    }

    private async void DeleteGoal_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string goalId)
        {
            var loc = LocalizationService.Current;
            var dialog = new ContentDialog
            {
                Title = loc.GetString("Goals_Delete_DialogTitle"),
                Content = loc.GetString("Goals_Delete_DialogContent"),
                PrimaryButtonText = loc.GetString("Common_Delete"),
                CloseButtonText = loc.GetString("Common_Cancel"),
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
