using System;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace FinPulse.Windows.Services;

public interface INotificationService
{
    void ShowNotification(string title, string message, string? tag = null);
    void NotifyBudgetAlert(string budgetName, int percentageConsumed);
    void NotifyUpcomingBill(string title, string amountFormatted, string dueDate);
}

public class WindowsNotificationService : INotificationService
{
    private readonly bool _isSupported;

    public WindowsNotificationService()
    {
        try
        {
            _isSupported = AppNotificationManager.IsSupported();
            if (_isSupported)
            {
                AppNotificationManager.Default.Register();
            }
        }
        catch
        {
            _isSupported = false;
        }
    }

    public void ShowNotification(string title, string message, string? tag = null)
    {
        if (!_isSupported) return;

        try
        {
            var notification = new AppNotificationBuilder()
                .AddText(title)
                .AddText(message)
                .BuildNotification();

            if (!string.IsNullOrEmpty(tag))
            {
                notification.Tag = tag;
            }

            AppNotificationManager.Default.Show(notification);
        }
        catch { }
    }

    public void NotifyBudgetAlert(string budgetName, int percentageConsumed)
    {
        ShowNotification(
            "Budget Alert",
            $"{budgetName} budget has reached {percentageConsumed}% of its limit.",
            tag: $"budget_{budgetName}"
        );
    }

    public void NotifyUpcomingBill(string title, string amountFormatted, string dueDate)
    {
        ShowNotification(
            "Upcoming Payment Due",
            $"{title} ({amountFormatted}) is scheduled for {dueDate}.",
            tag: $"bill_{title}"
        );
    }
}
