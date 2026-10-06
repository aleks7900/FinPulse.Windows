using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using FinPulse.Windows.Models;
using FinPulse.Windows.Repositories;
using FinPulse.Windows.Services;

namespace FinPulse.Windows.ViewModels;

public class RecurringViewModel : ViewModelBase
{
    private readonly IRecurringRepository _recurringRepo;
    private readonly IAccountRepository _accountRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly ITransactionRepository _txRepo;
    private readonly ILocalDataStore _store;

    public ObservableCollection<RecurringTransaction> RecurringRules { get; } = new();
    public ObservableCollection<Account> Accounts { get; } = new();
    public ObservableCollection<Category> Categories { get; } = new();

    private Money _totalMonthlyBurden = Money.Zero();
    public Money TotalMonthlyBurden
    {
        get => _totalMonthlyBurden;
        set
        {
            if (SetProperty(ref _totalMonthlyBurden, value))
            {
                OnPropertyChanged(nameof(TotalMonthlyBurdenString));
            }
        }
    }

    public string TotalMonthlyBurdenString => TotalMonthlyBurden.FormattedString;

    private Money _totalAnnualBurden = Money.Zero();
    public Money TotalAnnualBurden
    {
        get => _totalAnnualBurden;
        set
        {
            if (SetProperty(ref _totalAnnualBurden, value))
            {
                OnPropertyChanged(nameof(TotalAnnualBurdenString));
            }
        }
    }

    public string TotalAnnualBurdenString => TotalAnnualBurden.FormattedString;

    public IAsyncRelayCommand LoadRecurringCommand { get; }

    public RecurringViewModel(
        IRecurringRepository recurringRepo,
        IAccountRepository accountRepo,
        ICategoryRepository categoryRepo,
        ITransactionRepository txRepo,
        ILocalDataStore store)
    {
        _recurringRepo = recurringRepo;
        _accountRepo = accountRepo;
        _categoryRepo = categoryRepo;
        _txRepo = txRepo;
        _store = store;

        LoadRecurringCommand = new AsyncRelayCommand(LoadDataAsync);
        _store.DataChanged += (s, e) => _ = LoadDataAsync();
    }

    public async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            var accs = await _accountRepo.GetAllAsync();
            Accounts.Clear();
            foreach (var a in accs) Accounts.Add(a);

            var cats = await _categoryRepo.GetAllAsync();
            Categories.Clear();
            foreach (var c in cats) Categories.Add(c);

            var list = await _recurringRepo.GetAllAsync();
            RecurringRules.Clear();
            long monthlyMinor = 0;
            string cur = "USD";

            foreach (var r in list)
            {
                RecurringRules.Add(r);
                if (r.IsActive && !r.IsCancelled)
                {
                    monthlyMinor += r.CalculateMonthlyCost().AmountMinor;
                    cur = r.Amount.CurrencyCode;
                }
            }

            TotalMonthlyBurden = new Money(monthlyMinor, cur);
            TotalAnnualBurden = new Money(monthlyMinor * 12, cur);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task MarkAsPaidAsync(RecurringTransaction rule)
    {
        // 1. Create a transaction for this payment
        var tx = new Transaction
        {
            Id = Guid.NewGuid().ToString(),
            Amount = rule.Amount,
            Type = rule.Type,
            SourceAccountId = rule.AccountId,
            CategoryId = rule.CategoryId,
            Merchant = rule.Title,
            Description = LocalizationService.Current.Format("Recurring_TxDescription", rule.Title),
            RecurringRuleId = rule.Id,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        await _txRepo.SaveAsync(tx);

        // 2. Advance nextDueDate
        var currentDue = DateTimeOffset.FromUnixTimeMilliseconds(rule.NextDueDate);
        var nextDue = rule.Frequency switch
        {
            PaymentFrequency.DAILY => currentDue.AddDays(1),
            PaymentFrequency.WEEKLY => currentDue.AddDays(7),
            PaymentFrequency.BI_WEEKLY => currentDue.AddDays(14),
            PaymentFrequency.MONTHLY => currentDue.AddMonths(1),
            PaymentFrequency.QUARTERLY => currentDue.AddMonths(3),
            PaymentFrequency.YEARLY => currentDue.AddYears(1),
            _ => currentDue.AddMonths(1)
        };

        rule.LastProcessedDate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        rule.NextDueDate = nextDue.ToUnixTimeMilliseconds();

        await _recurringRepo.SaveAsync(rule);
        await LoadDataAsync();
    }

    public async Task AddRecurringRuleAsync(string title, decimal amount, PaymentFrequency frequency, string accountId, string categoryId, DateTimeOffset nextDue, bool isSubscription)
    {
        var settings = await _store.GetSettingsAsync();
        string cur = string.IsNullOrWhiteSpace(settings.BaseCurrencyCode) ? "EUR" : settings.BaseCurrencyCode;

        var rule = new RecurringTransaction
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            Amount = Money.FromMajor(amount, cur),
            Frequency = frequency,
            AccountId = accountId,
            CategoryId = categoryId,
            NextDueDate = nextDue.ToUnixTimeMilliseconds(),
            IsSubscription = isSubscription,
            IsActive = true
        };

        await _recurringRepo.SaveAsync(rule);
        await LoadDataAsync();
    }

    public async Task DeleteRuleAsync(string id)
    {
        await _recurringRepo.DeleteAsync(id);
        await LoadDataAsync();
    }
}
