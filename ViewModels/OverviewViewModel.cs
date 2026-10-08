using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using FinPulse.Windows.Models;
using FinPulse.Windows.Repositories;
using FinPulse.Windows.Services;

namespace FinPulse.Windows.ViewModels;

public class CashFlowBarItem
{
    public string MonthName { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
    public double IncomeHeightRatio { get; set; }
    public double ExpenseHeightRatio { get; set; }
    public double IncomeBarHeight => Math.Clamp(IncomeHeightRatio * 80.0, 6.0, 80.0);
    public double ExpenseBarHeight => Math.Clamp(ExpenseHeightRatio * 80.0, 6.0, 80.0);
    public string FormattedIncome { get; set; } = "$0";
    public string FormattedExpense { get; set; } = "$0";
}

public class OverviewViewModel : ViewModelBase
{
    private readonly ILocalDataStore _store;
    private readonly ISyncService _syncService;
    private readonly ICloudSyncCoordinator? _syncCoordinator;

    public ObservableCollection<Account> Accounts { get; } = new();
    public ObservableCollection<Transaction> RecentTransactions { get; } = new();
    public ObservableCollection<Budget> ActiveBudgets { get; } = new();
    public ObservableCollection<RecurringTransaction> UpcomingRecurring { get; } = new();
    public ObservableCollection<CashFlowBarItem> CashFlowBars { get; } = new();

    private Money _totalBalance = Money.Zero("USD");
    public Money TotalBalance
    {
        get => _totalBalance;
        set => SetProperty(ref _totalBalance, value);
    }

    private Money _availableBalance = Money.Zero("USD");
    public Money AvailableBalance
    {
        get => _availableBalance;
        set => SetProperty(ref _availableBalance, value);
    }

    private Money _monthlyIncome = Money.Zero("USD");
    public Money MonthlyIncome
    {
        get => _monthlyIncome;
        set => SetProperty(ref _monthlyIncome, value);
    }

    private Money _monthlyExpense = Money.Zero("USD");
    public Money MonthlyExpense
    {
        get => _monthlyExpense;
        set => SetProperty(ref _monthlyExpense, value);
    }

    private Money _netSavings = Money.Zero("USD");
    public Money NetSavings
    {
        get => _netSavings;
        set => SetProperty(ref _netSavings, value);
    }

    private string _currentMonthYear = DateTime.Now.ToString("MMMM yyyy", System.Globalization.CultureInfo.CurrentCulture);
    public string CurrentMonthYear
    {
        get => _currentMonthYear;
        set => SetProperty(ref _currentMonthYear, value);
    }

    public string RefreshTooltip => LocalizationService.Current.GetString("Overview_RefreshButton_Tooltip");

    public bool IsSyncing => _syncCoordinator?.IsSyncing ?? false;
    public bool IsNotSyncing => !IsSyncing;

    private string? _syncErrorMessage;
    public string? SyncErrorMessage
    {
        get => _syncErrorMessage;
        set
        {
            if (SetProperty(ref _syncErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasSyncError));
            }
        }
    }

    public bool HasSyncError => !string.IsNullOrEmpty(SyncErrorMessage);

    public IAsyncRelayCommand RefreshCommand { get; }

    public OverviewViewModel(ILocalDataStore store, ISyncService syncService, ICloudSyncCoordinator? syncCoordinator = null)
    {
        _store = store;
        _syncService = syncService;
        _syncCoordinator = syncCoordinator;

        RefreshCommand = new AsyncRelayCommand(PerformSyncAndRefreshAsync);
        _store.DataChanged += (s, e) => _ = LoadDataAsync();
        LocalizationService.Current.LanguageChanged += (s, e) =>
        {
            CurrentMonthYear = DateTime.Now.ToString("MMMM yyyy", System.Globalization.CultureInfo.CurrentCulture);
            OnPropertyChanged(nameof(RefreshTooltip));
            _ = LoadDataAsync();
        };

        if (_syncCoordinator != null)
        {
            _syncCoordinator.StateChanged += (s, state) =>
            {
                OnPropertyChanged(nameof(IsSyncing));
                OnPropertyChanged(nameof(IsNotSyncing));
            };
        }
    }

    public async Task PerformSyncAndRefreshAsync()
    {
        if (IsSyncing) return;

        SyncErrorMessage = null;
        IsBusy = true;
        try
        {
            if (_syncCoordinator != null)
            {
                var result = await _syncCoordinator.SyncAsync(SyncTrigger.Manual);
                if (!result.IsSuccess)
                {
                    SyncErrorMessage = result.ErrorMessage ?? "Synchronization failed";
                }
            }
            else
            {
                var result = await _syncService.PerformFullSyncAsync();
                if (!result.IsSuccess)
                {
                    SyncErrorMessage = result.ErrorMessage ?? "Synchronization failed";
                }
            }
        }
        catch (Exception ex)
        {
            SyncErrorMessage = ex.Message;
        }
        finally
        {
            await LoadDataAsync();
            IsBusy = false;
        }
    }

    public async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            var settings = await _store.GetSettingsAsync();
            string baseCurrency = string.IsNullOrWhiteSpace(settings.BaseCurrencyCode) ? "USD" : settings.BaseCurrencyCode;

            // 1. Accounts
            var allAccounts = await _store.GetAccountsAsync();
            Accounts.Clear();
            long totalBalanceMinor = 0;
            long totalAvailableMinor = 0;
            foreach (var acc in allAccounts)
            {
                Accounts.Add(acc);
                var convertedBalance = CurrencyConfig.Convert(acc.Balance, baseCurrency);
                var convertedAvailable = CurrencyConfig.Convert(acc.AvailableBalance, baseCurrency);
                totalBalanceMinor += convertedBalance.AmountMinor;
                totalAvailableMinor += convertedAvailable.AmountMinor;
            }
            TotalBalance = new Money(totalBalanceMinor, baseCurrency);
            AvailableBalance = new Money(totalAvailableMinor, baseCurrency);

            // 2. Transactions & Cash Flow
            var allTx = await _store.GetTransactionsAsync();
            RecentTransactions.Clear();
            foreach (var tx in allTx.Take(6))
            {
                RecentTransactions.Add(tx);
            }

            var now = DateTimeOffset.UtcNow;
            long startOfMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

            var currentMonthTx = allTx.Where(t => t.Timestamp >= startOfMonth).ToList();
            long incomeMinor = 0;
            long expenseMinor = 0;

            foreach (var t in currentMonthTx)
            {
                var convertedAmount = CurrencyConfig.Convert(t.Amount, baseCurrency);
                if (t.Type == TransactionType.INCOME || t.Type == TransactionType.REFUND)
                {
                    incomeMinor += convertedAmount.AmountMinor;
                }
                else if (t.Type == TransactionType.EXPENSE)
                {
                    expenseMinor += convertedAmount.AmountMinor;
                }
            }

            MonthlyIncome = new Money(incomeMinor, baseCurrency);
            MonthlyExpense = new Money(expenseMinor, baseCurrency);
            NetSavings = new Money(incomeMinor - expenseMinor, baseCurrency);

            // Cash Flow History (last 4 months)
            GenerateCashFlowBars(allTx, baseCurrency);

            // 3. Budgets
            var budgets = await _store.GetBudgetsAsync();
            ActiveBudgets.Clear();
            foreach (var b in budgets.Take(3))
            {
                ActiveBudgets.Add(b);
            }

            // 4. Upcoming Recurring
            var recurring = await _store.GetRecurringRulesAsync();
            UpcomingRecurring.Clear();
            foreach (var r in recurring.Take(4))
            {
                UpcomingRecurring.Add(r);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void GenerateCashFlowBars(System.Collections.Generic.List<Transaction> allTx, string currency)
    {
        CashFlowBars.Clear();
        var now = DateTime.UtcNow;

        decimal maxVal = 100m;
        var tempBars = new System.Collections.Generic.List<(string Name, decimal Inc, decimal Exp)>();

        for (int i = 3; i >= 0; i--)
        {
            var targetMonth = now.AddMonths(-i);
            var start = new DateTimeOffset(targetMonth.Year, targetMonth.Month, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            var end = new DateTimeOffset(targetMonth.Year, targetMonth.Month, DateTime.DaysInMonth(targetMonth.Year, targetMonth.Month), 23, 59, 59, TimeSpan.Zero).ToUnixTimeMilliseconds();

            var monthTx = allTx.Where(t => t.Timestamp >= start && t.Timestamp <= end).ToList();
            decimal inc = monthTx
                .Where(t => t.Type == TransactionType.INCOME || t.Type == TransactionType.REFUND)
                .Sum(t => CurrencyConfig.Convert(t.Amount, currency).AmountDecimal);
            decimal exp = monthTx
                .Where(t => t.Type == TransactionType.EXPENSE)
                .Sum(t => CurrencyConfig.Convert(t.Amount, currency).AmountDecimal);

            if (inc > maxVal) maxVal = inc;
            if (exp > maxVal) maxVal = exp;

            tempBars.Add((targetMonth.ToString("MMM", System.Globalization.CultureInfo.CurrentCulture), inc, exp));
        }

        foreach (var bar in tempBars)
        {
            double incRatio = (double)(bar.Inc / maxVal);
            double expRatio = (double)(bar.Exp / maxVal);
            CashFlowBars.Add(new CashFlowBarItem
            {
                MonthName = bar.Name,
                Income = bar.Inc,
                Expense = bar.Exp,
                IncomeHeightRatio = Math.Max(0.05, incRatio),
                ExpenseHeightRatio = Math.Max(0.05, expRatio),
                FormattedIncome = CurrencyConfig.Format(Money.FromMajor(bar.Inc, currency)),
                FormattedExpense = CurrencyConfig.Format(Money.FromMajor(bar.Exp, currency))
            });
        }
    }
}
