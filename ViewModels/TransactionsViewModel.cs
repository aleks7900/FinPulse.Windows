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

public class TransactionsViewModel : ViewModelBase
{
    private readonly ITransactionRepository _txRepo;
    private readonly IAccountRepository _accountRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly ILocalDataStore _store;

    private List<Transaction> _allTransactions = new();

    public ObservableCollection<Transaction> FilteredTransactions { get; } = new();
    public ObservableCollection<Account> Accounts { get; } = new();
    public ObservableCollection<Category> Categories { get; } = new();

    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                ApplyFilters();
            }
        }
    }

    private string _selectedTypeFilter = "All";
    public string SelectedTypeFilter
    {
        get => _selectedTypeFilter;
        set
        {
            if (SetProperty(ref _selectedTypeFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    private Account? _selectedAccountFilter;
    public Account? SelectedAccountFilter
    {
        get => _selectedAccountFilter;
        set
        {
            if (SetProperty(ref _selectedAccountFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    private Category? _selectedCategoryFilter;
    public Category? SelectedCategoryFilter
    {
        get => _selectedCategoryFilter;
        set
        {
            if (SetProperty(ref _selectedCategoryFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    private string _selectedDateFilter = "All Time";
    public string SelectedDateFilter
    {
        get => _selectedDateFilter;
        set
        {
            if (SetProperty(ref _selectedDateFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    private Transaction? _selectedTransaction;
    public Transaction? SelectedTransaction
    {
        get => _selectedTransaction;
        set => SetProperty(ref _selectedTransaction, value);
    }

    private int _totalTransactionsCount;
    public int TotalTransactionsCount
    {
        get => _totalTransactionsCount;
        set => SetProperty(ref _totalTransactionsCount, value);
    }

    private Money _filteredTotalAmount = Money.Zero();
    public Money FilteredTotalAmount
    {
        get => _filteredTotalAmount;
        set
        {
            if (SetProperty(ref _filteredTotalAmount, value))
            {
                OnPropertyChanged(nameof(FilteredTotalAmountString));
            }
        }
    }

    public string FilteredTotalAmountString => FilteredTotalAmount.FormattedString;

    public IAsyncRelayCommand LoadTransactionsCommand { get; }
    public IAsyncRelayCommand DeleteTransactionCommand { get; }

    public TransactionsViewModel(
        ITransactionRepository txRepo,
        IAccountRepository accountRepo,
        ICategoryRepository categoryRepo,
        ILocalDataStore store)
    {
        _txRepo = txRepo;
        _accountRepo = accountRepo;
        _categoryRepo = categoryRepo;
        _store = store;

        LoadTransactionsCommand = new AsyncRelayCommand(LoadDataAsync);
        DeleteTransactionCommand = new AsyncRelayCommand(DeleteSelectedAsync);

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

            _allTransactions = await _txRepo.GetAllAsync();
            TotalTransactionsCount = _allTransactions.Count;

            ApplyFilters();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ApplyFilters()
    {
        IEnumerable<Transaction> query = _allTransactions;

        // 1. Search Query (merchant, description, category, account)
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            string q = SearchQuery.Trim().ToLowerInvariant();
            query = query.Where(t =>
                (!string.IsNullOrEmpty(t.Merchant) && t.Merchant.ToLowerInvariant().Contains(q)) ||
                (!string.IsNullOrEmpty(t.Description) && t.Description.ToLowerInvariant().Contains(q)) ||
                (!string.IsNullOrEmpty(t.CategoryName) && t.CategoryName.ToLowerInvariant().Contains(q)) ||
                (!string.IsNullOrEmpty(t.SourceAccountName) && t.SourceAccountName.ToLowerInvariant().Contains(q)) ||
                (!string.IsNullOrEmpty(t.Notes) && t.Notes.ToLowerInvariant().Contains(q))
            );
        }

        // 2. Type Filter
        if (SelectedTypeFilter != "All")
        {
            if (Enum.TryParse<TransactionType>(SelectedTypeFilter.ToUpperInvariant(), out var type))
            {
                query = query.Where(t => t.Type == type);
            }
        }

        // 3. Account Filter
        if (SelectedAccountFilter != null)
        {
            query = query.Where(t => t.SourceAccountId == SelectedAccountFilter.Id || t.DestinationAccountId == SelectedAccountFilter.Id);
        }

        // 4. Category Filter
        if (SelectedCategoryFilter != null)
        {
            query = query.Where(t => t.CategoryId == SelectedCategoryFilter.Id);
        }

        // 5. Date Filter
        var now = DateTime.UtcNow;
        if (SelectedDateFilter == "This Month")
        {
            var start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            query = query.Where(t => t.Timestamp >= start);
        }
        else if (SelectedDateFilter == "Last Month")
        {
            var lastMonth = now.AddMonths(-1);
            var start = new DateTimeOffset(lastMonth.Year, lastMonth.Month, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            var end = new DateTimeOffset(lastMonth.Year, lastMonth.Month, DateTime.DaysInMonth(lastMonth.Year, lastMonth.Month), 23, 59, 59, TimeSpan.Zero).ToUnixTimeMilliseconds();
            query = query.Where(t => t.Timestamp >= start && t.Timestamp <= end);
        }
        else if (SelectedDateFilter == "This Year")
        {
            var start = new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            query = query.Where(t => t.Timestamp >= start);
        }

        var results = query.OrderByDescending(t => t.Timestamp).ToList();
        FilteredTransactions.Clear();
        long sumMinor = 0;
        string currency = "USD";

        foreach (var t in results)
        {
            FilteredTransactions.Add(t);
            currency = t.Amount.CurrencyCode;
            if (t.Type == TransactionType.EXPENSE)
                sumMinor -= t.Amount.AmountMinor;
            else if (t.Type == TransactionType.INCOME || t.Type == TransactionType.REFUND)
                sumMinor += t.Amount.AmountMinor;
        }

        FilteredTotalAmount = new Money(sumMinor, currency);
    }

    private async Task DeleteSelectedAsync()
    {
        if (SelectedTransaction == null) return;
        await _txRepo.DeleteAsync(SelectedTransaction.Id);
        await LoadDataAsync();
    }
}
