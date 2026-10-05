using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using FinPulse.Windows.Models;
using FinPulse.Windows.Repositories;
using FinPulse.Windows.Services;

namespace FinPulse.Windows.ViewModels;

public class QuickAddViewModel : ViewModelBase
{
    private readonly ITransactionRepository _txRepo;
    private readonly IAccountRepository _accountRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly ILocalDataStore _store;

    public ObservableCollection<Account> Accounts { get; } = new();
    public ObservableCollection<Category> Categories { get; } = new();

    private decimal _amount = 0.0m;
    public decimal Amount
    {
        get => _amount;
        set => SetProperty(ref _amount, value);
    }

    private TransactionType _selectedType = TransactionType.EXPENSE;
    public TransactionType SelectedType
    {
        get => _selectedType;
        set
        {
            if (SetProperty(ref _selectedType, value))
            {
                OnPropertyChanged(nameof(IsTransfer));
                _ = FilterCategoriesForType();
            }
        }
    }

    public bool IsTransfer => SelectedType == TransactionType.TRANSFER;

    private Account? _selectedSourceAccount;
    public Account? SelectedSourceAccount
    {
        get => _selectedSourceAccount;
        set => SetProperty(ref _selectedSourceAccount, value);
    }

    private Account? _selectedDestinationAccount;
    public Account? SelectedDestinationAccount
    {
        get => _selectedDestinationAccount;
        set => SetProperty(ref _selectedDestinationAccount, value);
    }

    private Category? _selectedCategory;
    public Category? SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }

    private string _description = string.Empty;
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    private string _merchant = string.Empty;
    public string Merchant
    {
        get => _merchant;
        set => SetProperty(ref _merchant, value);
    }

    private DateTimeOffset _transactionDate = DateTimeOffset.Now;
    public DateTimeOffset TransactionDate
    {
        get => _transactionDate;
        set => SetProperty(ref _transactionDate, value);
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public QuickAddViewModel(
        ITransactionRepository txRepo,
        IAccountRepository accountRepo,
        ICategoryRepository categoryRepo,
        ILocalDataStore store)
    {
        _txRepo = txRepo;
        _accountRepo = accountRepo;
        _categoryRepo = categoryRepo;
        _store = store;
    }

    public async Task InitializeAsync()
    {
        var accs = await _accountRepo.GetAllAsync();
        Accounts.Clear();
        foreach (var a in accs) Accounts.Add(a);
        SelectedSourceAccount = Accounts.FirstOrDefault();
        SelectedDestinationAccount = Accounts.Skip(1).FirstOrDefault();

        await FilterCategoriesForType();
        Amount = 0.0m;
        Description = string.Empty;
        Merchant = string.Empty;
        TransactionDate = DateTimeOffset.Now;
        ErrorMessage = null;
    }

    private async Task FilterCategoriesForType()
    {
        var allCats = await _categoryRepo.GetAllAsync();
        Categories.Clear();
        var targetType = SelectedType == TransactionType.INCOME ? CategoryType.INCOME : CategoryType.EXPENSE;

        foreach (var c in allCats.Where(c => c.Type == targetType))
        {
            Categories.Add(c);
        }

        SelectedCategory = Categories.FirstOrDefault();
    }

    public async Task<bool> SaveTransactionAsync()
    {
        ErrorMessage = null;
        if (Amount <= 0)
        {
            ErrorMessage = "Please enter an amount greater than 0.";
            return false;
        }

        if (SelectedSourceAccount == null)
        {
            ErrorMessage = "Please select a source account.";
            return false;
        }

        if (IsTransfer && SelectedDestinationAccount == null)
        {
            ErrorMessage = "Please select a destination account for transfer.";
            return false;
        }

        if (IsTransfer && SelectedSourceAccount.Id == SelectedDestinationAccount?.Id)
        {
            ErrorMessage = "Source and destination accounts must be different.";
            return false;
        }

        var money = Money.FromMajor(Amount, SelectedSourceAccount.CurrencyCode);
        string catId = SelectedCategory?.Id ?? (SelectedType == TransactionType.INCOME ? "cat_income_salary" : "cat_groceries");

        var tx = new Transaction
        {
            Id = Guid.NewGuid().ToString(),
            Amount = money,
            Type = SelectedType,
            SourceAccountId = SelectedSourceAccount.Id,
            DestinationAccountId = IsTransfer ? SelectedDestinationAccount?.Id : null,
            CategoryId = catId,
            Merchant = string.IsNullOrWhiteSpace(Merchant) ? null : Merchant.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? (Merchant ?? "Quick Transaction") : Description.Trim(),
            Timestamp = TransactionDate.ToUnixTimeMilliseconds()
        };

        await _txRepo.SaveAsync(tx);
        return true;
    }
}
