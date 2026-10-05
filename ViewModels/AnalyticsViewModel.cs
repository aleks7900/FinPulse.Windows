using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using FinPulse.Windows.Models;
using FinPulse.Windows.Repositories;
using FinPulse.Windows.Services;

namespace FinPulse.Windows.ViewModels;

public class CategorySpendItem
{
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryIcon { get; set; } = "category";
    public string HexColor { get; set; } = "#2E7D32";
    public Money Amount { get; set; } = Money.Zero();
    public double Percentage { get; set; }
    public string FormattedPercentage => $"{Percentage:F1}%";
    public double BarWidthRatio => Math.Clamp(Percentage / 100.0, 0.02, 1.0);
    public string FormattedAmount => Amount.FormattedString;
}

public class MonthlyTrendItem
{
    public string PeriodLabel { get; set; } = string.Empty;
    public Money Income { get; set; } = Money.Zero();
    public Money Expense { get; set; } = Money.Zero();
    public Money Net { get; set; } = Money.Zero();
    public double IncomeRatio { get; set; }
    public double ExpenseRatio { get; set; }
    public string FormattedNet => Net.FormattedString;
    public double IncomePercent => Math.Clamp(IncomeRatio * 100.0, 0.0, 100.0);
    public double ExpensePercent => Math.Clamp(ExpenseRatio * 100.0, 0.0, 100.0);
}

public class AnalyticsViewModel : ViewModelBase
{
    private readonly ITransactionRepository _txRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly ILocalDataStore _store;

    public ObservableCollection<CategorySpendItem> CategoryBreakdown { get; } = new();
    public ObservableCollection<MonthlyTrendItem> MonthlyTrends { get; } = new();

    private string _selectedPeriod = "30 Days";
    public string SelectedPeriod
    {
        get => _selectedPeriod;
        set
        {
            if (SetProperty(ref _selectedPeriod, value))
            {
                _ = LoadAnalyticsAsync();
            }
        }
    }

    private Money _periodIncome = Money.Zero();
    public Money PeriodIncome
    {
        get => _periodIncome;
        set
        {
            if (SetProperty(ref _periodIncome, value))
            {
                OnPropertyChanged(nameof(PeriodIncomeString));
            }
        }
    }

    public string PeriodIncomeString => PeriodIncome.FormattedString;

    private Money _periodExpense = Money.Zero();
    public Money PeriodExpense
    {
        get => _periodExpense;
        set
        {
            if (SetProperty(ref _periodExpense, value))
            {
                OnPropertyChanged(nameof(PeriodExpenseString));
            }
        }
    }

    public string PeriodExpenseString => PeriodExpense.FormattedString;

    private Money _periodNetSavings = Money.Zero();
    public Money PeriodNetSavings
    {
        get => _periodNetSavings;
        set
        {
            if (SetProperty(ref _periodNetSavings, value))
            {
                OnPropertyChanged(nameof(PeriodNetSavingsString));
            }
        }
    }

    public string PeriodNetSavingsString => PeriodNetSavings.FormattedString;

    private double _savingsRate = 0.0;
    public double SavingsRate
    {
        get => _savingsRate;
        set => SetProperty(ref _savingsRate, value);
    }

    private Money _averageDailyBurn = Money.Zero();
    public Money AverageDailyBurn
    {
        get => _averageDailyBurn;
        set
        {
            if (SetProperty(ref _averageDailyBurn, value))
            {
                OnPropertyChanged(nameof(AverageDailyBurnString));
            }
        }
    }

    public string AverageDailyBurnString => AverageDailyBurn.FormattedString;

    private Money _largestExpense = Money.Zero();
    public Money LargestExpense
    {
        get => _largestExpense;
        set => SetProperty(ref _largestExpense, value);
    }

    public IAsyncRelayCommand LoadAnalyticsCommand { get; }

    public AnalyticsViewModel(ITransactionRepository txRepo, ICategoryRepository categoryRepo, ILocalDataStore store)
    {
        _txRepo = txRepo;
        _categoryRepo = categoryRepo;
        _store = store;

        LoadAnalyticsCommand = new AsyncRelayCommand(LoadAnalyticsAsync);
        _store.DataChanged += (s, e) => _ = LoadAnalyticsAsync();
    }

    public async Task LoadAnalyticsAsync()
    {
        IsBusy = true;
        try
        {
            var settings = await _store.GetSettingsAsync();
            string cur = string.IsNullOrWhiteSpace(settings.BaseCurrencyCode) ? "EUR" : settings.BaseCurrencyCode;

            var allTx = await _txRepo.GetAllAsync();
            var allCats = (await _categoryRepo.GetAllAsync()).ToDictionary(c => c.Id, c => c);

            var now = DateTime.UtcNow;
            long cutoffTimestamp = SelectedPeriod switch
            {
                "7 Days" => DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeMilliseconds(),
                "30 Days" => DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeMilliseconds(),
                "3 Months" => DateTimeOffset.UtcNow.AddMonths(-3).ToUnixTimeMilliseconds(),
                "6 Months" => DateTimeOffset.UtcNow.AddMonths(-6).ToUnixTimeMilliseconds(),
                "1 Year" => DateTimeOffset.UtcNow.AddYears(-1).ToUnixTimeMilliseconds(),
                _ => 0L
            };

            int days = SelectedPeriod switch
            {
                "7 Days" => 7,
                "30 Days" => 30,
                "3 Months" => 90,
                "6 Months" => 180,
                "1 Year" => 365,
                _ => 365
            };

            var filteredTx = allTx.Where(t => t.Timestamp >= cutoffTimestamp).ToList();

            long incMinor = filteredTx
                .Where(t => t.Type == TransactionType.INCOME || t.Type == TransactionType.REFUND)
                .Sum(t => t.Amount.AmountMinor);

            long expMinor = filteredTx
                .Where(t => t.Type == TransactionType.EXPENSE)
                .Sum(t => t.Amount.AmountMinor);

            PeriodIncome = new Money(incMinor, cur);
            PeriodExpense = new Money(expMinor, cur);
            PeriodNetSavings = new Money(incMinor - expMinor, cur);

            SavingsRate = incMinor > 0 ? Math.Max(0.0, ((double)(incMinor - expMinor) / incMinor) * 100.0) : 0.0;
            AverageDailyBurn = new Money(days > 0 ? expMinor / days : 0, cur);

            var largestTx = filteredTx.Where(t => t.Type == TransactionType.EXPENSE).OrderByDescending(t => t.Amount.AmountMinor).FirstOrDefault();
            LargestExpense = largestTx?.Amount ?? Money.Zero(cur);

            // Category breakdown
            CategoryBreakdown.Clear();
            var expenseByCat = filteredTx
                .Where(t => t.Type == TransactionType.EXPENSE)
                .GroupBy(t => t.CategoryId)
                .Select(g => new
                {
                    CategoryId = g.Key,
                    TotalMinor = g.Sum(t => t.Amount.AmountMinor)
                })
                .OrderByDescending(x => x.TotalMinor)
                .ToList();

            foreach (var item in expenseByCat)
            {
                allCats.TryGetValue(item.CategoryId, out var cat);
                double pct = expMinor > 0 ? ((double)item.TotalMinor / expMinor) * 100.0 : 0.0;

                CategoryBreakdown.Add(new CategorySpendItem
                {
                    CategoryName = cat?.Name ?? "Uncategorized",
                    CategoryIcon = cat?.Icon ?? "category",
                    HexColor = cat != null ? $"#{((uint)cat.ColorHex):X8}" : "#607D8B",
                    Amount = new Money(item.TotalMinor, cur),
                    Percentage = pct
                });
            }

            // Monthly Trend (last 6 months)
            MonthlyTrends.Clear();
            decimal maxTrend = 100m;
            var trendTemp = new List<(string Label, decimal Inc, decimal Exp, decimal Net)>();

            for (int i = 5; i >= 0; i--)
            {
                var targetMonth = now.AddMonths(-i);
                var start = new DateTimeOffset(targetMonth.Year, targetMonth.Month, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
                var end = new DateTimeOffset(targetMonth.Year, targetMonth.Month, DateTime.DaysInMonth(targetMonth.Year, targetMonth.Month), 23, 59, 59, TimeSpan.Zero).ToUnixTimeMilliseconds();

                var mTx = allTx.Where(t => t.Timestamp >= start && t.Timestamp <= end).ToList();
                decimal inc = mTx.Where(t => t.Type == TransactionType.INCOME).Sum(t => t.Amount.AmountDecimal);
                decimal exp = mTx.Where(t => t.Type == TransactionType.EXPENSE).Sum(t => t.Amount.AmountDecimal);

                if (inc > maxTrend) maxTrend = inc;
                if (exp > maxTrend) maxTrend = exp;

                trendTemp.Add((targetMonth.ToString("MMM yyyy"), inc, exp, inc - exp));
            }

            foreach (var (label, inc, exp, net) in trendTemp)
            {
                MonthlyTrends.Add(new MonthlyTrendItem
                {
                    PeriodLabel = label,
                    Income = Money.FromMajor(inc, cur),
                    Expense = Money.FromMajor(exp, cur),
                    Net = Money.FromMajor(net, cur),
                    IncomeRatio = Math.Max(0.04, (double)(inc / maxTrend)),
                    ExpenseRatio = Math.Max(0.04, (double)(exp / maxTrend))
                });
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
