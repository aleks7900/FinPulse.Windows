using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using FinPulse.Windows.Models;
using FinPulse.Windows.Repositories;
using FinPulse.Windows.Services;

namespace FinPulse.Windows.ViewModels;

public class BudgetsViewModel : ViewModelBase
{
    private readonly IBudgetRepository _budgetRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly ILocalDataStore _store;

    public ObservableCollection<Budget> Budgets { get; } = new();
    public ObservableCollection<Category> AvailableCategories { get; } = new();

    private Money _totalBudgetLimit = Money.Zero();
    public Money TotalBudgetLimit
    {
        get => _totalBudgetLimit;
        set
        {
            if (SetProperty(ref _totalBudgetLimit, value))
            {
                OnPropertyChanged(nameof(TotalBudgetLimitString));
            }
        }
    }

    public string TotalBudgetLimitString => TotalBudgetLimit.FormattedString;

    private Money _totalBudgetSpent = Money.Zero();
    public Money TotalBudgetSpent
    {
        get => _totalBudgetSpent;
        set
        {
            if (SetProperty(ref _totalBudgetSpent, value))
            {
                OnPropertyChanged(nameof(TotalBudgetSpentString));
            }
        }
    }

    public string TotalBudgetSpentString => TotalBudgetSpent.FormattedString;

    private Money _totalBudgetRemaining = Money.Zero();
    public Money TotalBudgetRemaining
    {
        get => _totalBudgetRemaining;
        set
        {
            if (SetProperty(ref _totalBudgetRemaining, value))
            {
                OnPropertyChanged(nameof(TotalBudgetRemainingString));
            }
        }
    }

    public string TotalBudgetRemainingString => TotalBudgetRemaining.FormattedString;

    public IAsyncRelayCommand LoadBudgetsCommand { get; }

    public BudgetsViewModel(IBudgetRepository budgetRepo, ICategoryRepository categoryRepo, ILocalDataStore store)
    {
        _budgetRepo = budgetRepo;
        _categoryRepo = categoryRepo;
        _store = store;

        LoadBudgetsCommand = new AsyncRelayCommand(LoadBudgetsAsync);
        _store.DataChanged += (s, e) => _ = LoadBudgetsAsync();
    }

    public async Task LoadBudgetsAsync()
    {
        IsBusy = true;
        try
        {
            var cats = await _categoryRepo.GetAllAsync();
            AvailableCategories.Clear();
            foreach (var c in cats.Where(c => c.Type == CategoryType.EXPENSE))
            {
                AvailableCategories.Add(c);
            }

            var list = await _budgetRepo.GetAllAsync();
            Budgets.Clear();
            long totalLimitMinor = 0;
            long totalSpentMinor = 0;
            string currency = "USD";

            foreach (var b in list)
            {
                Budgets.Add(b);
                totalLimitMinor += b.EffectiveLimit.AmountMinor;
                totalSpentMinor += b.SpentAmount.AmountMinor;
                currency = b.LimitAmount.CurrencyCode;
            }

            TotalBudgetLimit = new Money(totalLimitMinor, currency);
            TotalBudgetSpent = new Money(totalSpentMinor, currency);
            TotalBudgetRemaining = new Money(Math.Max(0, totalLimitMinor - totalSpentMinor), currency);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task AddBudgetAsync(string name, string categoryId, decimal limitAmount, BudgetPeriod period)
    {
        var settings = await _store.GetSettingsAsync();
        string cur = string.IsNullOrWhiteSpace(settings.BaseCurrencyCode) ? "EUR" : settings.BaseCurrencyCode;
        var limit = Money.FromMajor(limitAmount, cur);

        var now = DateTimeOffset.UtcNow;
        var budget = new Budget
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            CategoryId = categoryId,
            LimitAmount = limit,
            PeriodType = period,
            StartDate = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            EndDate = new DateTimeOffset(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month), 23, 59, 59, TimeSpan.Zero).ToUnixTimeMilliseconds()
        };

        await _budgetRepo.SaveAsync(budget);
        await LoadBudgetsAsync();
    }

    public async Task DeleteBudgetAsync(string id)
    {
        await _budgetRepo.DeleteAsync(id);
        await LoadBudgetsAsync();
    }
}
