using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using FinPulse.Windows.Models;
using FinPulse.Windows.Repositories;
using FinPulse.Windows.Services;

namespace FinPulse.Windows.ViewModels;

public class AccountsViewModel : ViewModelBase
{
    private readonly IAccountRepository _accountRepo;
    private readonly ITransactionRepository _txRepo;
    private readonly ILocalDataStore _store;

    public ObservableCollection<Account> Accounts { get; } = new();
    public ObservableCollection<Transaction> SelectedAccountTransactions { get; } = new();

    private Account? _selectedAccount;
    public Account? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (SetProperty(ref _selectedAccount, value))
            {
                OnPropertyChanged(nameof(HasSelectedAccount));
                OnPropertyChanged(nameof(EmptyStateVisibility));
                OnPropertyChanged(nameof(DetailsVisibility));
                OnPropertyChanged(nameof(SelectedAccountFormattedBalance));
                OnPropertyChanged(nameof(SelectedAccountName));
                OnPropertyChanged(nameof(SelectedAccountInstitution));
                _ = LoadTransactionsForSelectedAccountAsync();
            }
        }
    }

    public bool HasSelectedAccount => SelectedAccount != null;
    public Microsoft.UI.Xaml.Visibility EmptyStateVisibility => HasSelectedAccount ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
    public Microsoft.UI.Xaml.Visibility DetailsVisibility => HasSelectedAccount ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public string SelectedAccountFormattedBalance => SelectedAccount?.FormattedBalance ?? "$0.00";
    public string SelectedAccountName => SelectedAccount?.Name ?? string.Empty;
    public string SelectedAccountInstitution => SelectedAccount?.Institution ?? string.Empty;

    private Money _selectedAccountIncome = Money.Zero();
    public Money SelectedAccountIncome
    {
        get => _selectedAccountIncome;
        set
        {
            if (SetProperty(ref _selectedAccountIncome, value))
            {
                OnPropertyChanged(nameof(SelectedAccountIncomeString));
            }
        }
    }

    public string SelectedAccountIncomeString => SelectedAccountIncome.FormattedString;

    private Money _selectedAccountExpense = Money.Zero();
    public Money SelectedAccountExpense
    {
        get => _selectedAccountExpense;
        set
        {
            if (SetProperty(ref _selectedAccountExpense, value))
            {
                OnPropertyChanged(nameof(SelectedAccountExpenseString));
            }
        }
    }

    public string SelectedAccountExpenseString => SelectedAccountExpense.FormattedString;

    public IAsyncRelayCommand LoadAccountsCommand { get; }
    public IAsyncRelayCommand DeleteAccountCommand { get; }

    public AccountsViewModel(IAccountRepository accountRepo, ITransactionRepository txRepo, ILocalDataStore store)
    {
        _accountRepo = accountRepo;
        _txRepo = txRepo;
        _store = store;

        LoadAccountsCommand = new AsyncRelayCommand(LoadAccountsAsync);
        DeleteAccountCommand = new AsyncRelayCommand(DeleteSelectedAccountAsync);

        _store.DataChanged += (s, e) => _ = LoadAccountsAsync();
    }

    public async Task LoadAccountsAsync()
    {
        IsBusy = true;
        try
        {
            var list = await _accountRepo.GetAllAsync();
            Accounts.Clear();
            foreach (var acc in list)
            {
                Accounts.Add(acc);
            }

            if (SelectedAccount == null || !Accounts.Any(a => a.Id == SelectedAccount.Id))
            {
                SelectedAccount = Accounts.FirstOrDefault();
            }
            else
            {
                // Update reference to updated object
                SelectedAccount = Accounts.First(a => a.Id == SelectedAccount.Id);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadTransactionsForSelectedAccountAsync()
    {
        if (SelectedAccount == null)
        {
            SelectedAccountTransactions.Clear();
            SelectedAccountIncome = Money.Zero();
            SelectedAccountExpense = Money.Zero();
            return;
        }

        var allTx = await _txRepo.GetAllAsync();
        var forAccount = allTx
            .Where(t => t.SourceAccountId == SelectedAccount.Id || t.DestinationAccountId == SelectedAccount.Id)
            .OrderByDescending(t => t.Timestamp)
            .ToList();

        SelectedAccountTransactions.Clear();
        long incMinor = 0;
        long expMinor = 0;

        foreach (var t in forAccount)
        {
            SelectedAccountTransactions.Add(t);
            if (t.SourceAccountId == SelectedAccount.Id)
            {
                if (t.Type == TransactionType.INCOME || t.Type == TransactionType.REFUND)
                    incMinor += t.Amount.AmountMinor;
                else if (t.Type == TransactionType.EXPENSE || t.Type == TransactionType.TRANSFER)
                    expMinor += t.Amount.AmountMinor;
            }
            else if (t.DestinationAccountId == SelectedAccount.Id && t.Type == TransactionType.TRANSFER)
            {
                incMinor += (t.DestinationAmount ?? t.Amount).AmountMinor;
            }
        }

        SelectedAccountIncome = new Money(incMinor, SelectedAccount.CurrencyCode);
        SelectedAccountExpense = new Money(expMinor, SelectedAccount.CurrencyCode);
    }

    public async Task AddAccountAsync(string name, AccountType type, decimal initialBalance, string currencyCode, string institution)
    {
        var money = Money.FromMajor(initialBalance, currencyCode);
        var acc = new Account
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Type = type,
            Balance = money,
            AvailableBalance = money,
            Institution = institution,
            ColorHex = type switch
            {
                AccountType.SAVINGS => 0xFF2196F3,
                AccountType.CREDIT_CARD => 0xFFE91E63,
                AccountType.INVESTMENT => 0xFF9C27B0,
                AccountType.CASH => 0xFF4CAF50,
                _ => 0xFF2E7D32
            }
        };

        await _accountRepo.SaveAsync(acc);
        await LoadAccountsAsync();
        SelectedAccount = Accounts.FirstOrDefault(a => a.Id == acc.Id);
    }

    public async Task TransferFundsAsync(string sourceId, string destinationId, decimal amount, string? note = null)
    {
        var src = await _accountRepo.GetByIdAsync(sourceId);
        if (src == null) return;

        var money = Money.FromMajor(amount, src.CurrencyCode);
        await _accountRepo.TransferAsync(sourceId, destinationId, money, note);
        await LoadAccountsAsync();
    }

    private async Task DeleteSelectedAccountAsync()
    {
        if (SelectedAccount == null) return;
        await _accountRepo.DeleteAsync(SelectedAccount.Id);
        await LoadAccountsAsync();
    }
}
