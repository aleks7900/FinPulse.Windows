using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FinPulse.Windows.Models;

namespace FinPulse.Windows.Services;

public class LocalDataStore : ILocalDataStore
{
    private readonly string _dataDir;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private readonly ConcurrentDictionary<string, Account> _accounts = new();
    private readonly ConcurrentDictionary<string, Transaction> _transactions = new();
    private readonly ConcurrentDictionary<string, Category> _categories = new();
    private readonly ConcurrentDictionary<string, Budget> _budgets = new();
    private readonly ConcurrentDictionary<string, RecurringTransaction> _recurringRules = new();
    private readonly ConcurrentDictionary<string, FinancialGoal> _goals = new();
    private readonly ConcurrentDictionary<string, SyncQueueItem> _syncQueue = new();
    private CloudSettings _settings = new();

    public event EventHandler? DataChanged;

    public LocalDataStore(string? baseDir = null)
    {
        if (!string.IsNullOrEmpty(baseDir))
        {
            _dataDir = baseDir;
        }
        else
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _dataDir = Path.Combine(localAppData, "FinPulseCompanion", "store");
        }
        Directory.CreateDirectory(_dataDir);
    }

    public async Task InitializeAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await LoadFromFileAsync("settings.json", data => _settings = JsonSerializer.Deserialize<CloudSettings>(data) ?? new CloudSettings());
            await LoadDictionaryAsync("accounts.json", _accounts);
            await LoadDictionaryAsync("categories.json", _categories);
            await LoadDictionaryAsync("transactions.json", _transactions);
            await LoadDictionaryAsync("budgets.json", _budgets);
            await LoadDictionaryAsync("recurring.json", _recurringRules);
            await LoadDictionaryAsync("goals.json", _goals);
            await LoadDictionaryAsync("sync_queue.json", _syncQueue);

            // Populate default categories if empty
            if (_categories.IsEmpty)
            {
                var defaults = DefaultCategoryCatalog.GetDefaultCategories();
                foreach (var cat in defaults)
                {
                    _categories[cat.Id] = cat;
                }
                await SaveDictionaryAsync("categories.json", _categories);
            }

            // Seed initial account and sample data if clean install
            if (_accounts.IsEmpty)
            {
                await SeedInitialDataInternalAsync();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SeedInitialDataInternalAsync()
    {
        string cur = _settings.BaseCurrencyCode;
        if (string.IsNullOrWhiteSpace(cur)) cur = "EUR";

        var mainAccount = new Account
        {
            Id = "acc_main",
            Name = "Main Checking",
            Type = AccountType.BANK,
            Balance = Money.FromMajor(8240.25m, cur),
            AvailableBalance = Money.FromMajor(8240.25m, cur),
            ColorHex = 0xFF2E7D32,
            Institution = "ING Bank",
            Icon = "account_balance"
        };

        var savingsAccount = new Account
        {
            Id = "acc_savings",
            Name = "Emergency Savings",
            Type = AccountType.SAVINGS,
            Balance = Money.FromMajor(4300.00m, cur),
            AvailableBalance = Money.FromMajor(4300.00m, cur),
            ColorHex = 0xFF2196F3,
            Institution = "ING Bank",
            Icon = "savings"
        };

        _accounts[mainAccount.Id] = mainAccount;
        _accounts[savingsAccount.Id] = savingsAccount;

        // Sample transactions
        var now = DateTimeOffset.UtcNow;
        var t1 = new Transaction
        {
            Id = "tx_1",
            Amount = Money.FromMajor(3500.00m, cur),
            Type = TransactionType.INCOME,
            SourceAccountId = mainAccount.Id,
            CategoryId = "cat_income_salary",
            Merchant = "Acme Corp Tech",
            Description = "Monthly Engineering Salary",
            Timestamp = now.AddDays(-2).ToUnixTimeMilliseconds()
        };

        var t2 = new Transaction
        {
            Id = "tx_2",
            Amount = Money.FromMajor(84.30m, cur),
            Type = TransactionType.EXPENSE,
            SourceAccountId = mainAccount.Id,
            CategoryId = "cat_groceries",
            Merchant = "Whole Foods Market",
            Description = "Weekly Groceries & Produce",
            Timestamp = now.AddDays(-1).ToUnixTimeMilliseconds()
        };

        var t3 = new Transaction
        {
            Id = "tx_3",
            Amount = Money.FromMajor(52.10m, cur),
            Type = TransactionType.EXPENSE,
            SourceAccountId = mainAccount.Id,
            CategoryId = "cat_fuel",
            Merchant = "Shell Oil",
            Description = "Full Tank Fuel",
            Timestamp = now.AddHours(-18).ToUnixTimeMilliseconds()
        };

        var t4 = new Transaction
        {
            Id = "tx_4",
            Amount = Money.FromMajor(9.99m, cur),
            Type = TransactionType.EXPENSE,
            SourceAccountId = mainAccount.Id,
            CategoryId = "cat_subscriptions",
            Merchant = "Spotify",
            Description = "Spotify Premium Monthly",
            Timestamp = now.AddHours(-6).ToUnixTimeMilliseconds()
        };

        _transactions[t1.Id] = t1;
        _transactions[t2.Id] = t2;
        _transactions[t3.Id] = t3;
        _transactions[t4.Id] = t4;

        // Sample Budgets
        var b1 = new Budget
        {
            Id = "b_food",
            CategoryId = "cat_food",
            Name = "Food & Dining",
            LimitAmount = Money.FromMajor(600.00m, cur),
            SpentAmount = Money.FromMajor(420.00m, cur),
            PeriodType = BudgetPeriod.MONTHLY
        };

        var b2 = new Budget
        {
            Id = "b_shopping",
            CategoryId = "cat_shopping",
            Name = "Shopping",
            LimitAmount = Money.FromMajor(350.00m, cur),
            SpentAmount = Money.FromMajor(145.00m, cur),
            PeriodType = BudgetPeriod.MONTHLY
        };

        _budgets[b1.Id] = b1;
        _budgets[b2.Id] = b2;

        // Sample Recurring Rules
        var r1 = new RecurringTransaction
        {
            Id = "rec_internet",
            Title = "Fiber Internet",
            Amount = Money.FromMajor(25.00m, cur),
            Type = TransactionType.EXPENSE,
            AccountId = mainAccount.Id,
            CategoryId = "cat_home_internet",
            Frequency = PaymentFrequency.MONTHLY,
            NextDueDate = now.AddDays(3).ToUnixTimeMilliseconds(),
            IsSubscription = true
        };

        var r2 = new RecurringTransaction
        {
            Id = "rec_netflix",
            Title = "Netflix 4K",
            Amount = Money.FromMajor(15.00m, cur),
            Type = TransactionType.EXPENSE,
            AccountId = mainAccount.Id,
            CategoryId = "cat_subscriptions",
            Frequency = PaymentFrequency.MONTHLY,
            NextDueDate = now.AddDays(6).ToUnixTimeMilliseconds(),
            IsSubscription = true
        };

        _recurringRules[r1.Id] = r1;
        _recurringRules[r2.Id] = r2;

        // Sample Goal
        var g1 = new FinancialGoal
        {
            Id = "g_laptop",
            Title = "New MacBook Pro M4",
            TargetAmount = Money.FromMajor(2000.00m, cur),
            CurrentAmount = Money.FromMajor(1350.00m, cur),
            TargetDate = now.AddMonths(4).ToUnixTimeMilliseconds(),
            LinkedAccountId = savingsAccount.Id,
            ColorHex = 0xFF43A047
        };

        _goals[g1.Id] = g1;

        await SaveDictionaryAsync("accounts.json", _accounts);
        await SaveDictionaryAsync("transactions.json", _transactions);
        await SaveDictionaryAsync("budgets.json", _budgets);
        await SaveDictionaryAsync("recurring.json", _recurringRules);
        await SaveDictionaryAsync("goals.json", _goals);
    }

    #region Accounts
    public Task<List<Account>> GetAccountsAsync()
    {
        return Task.FromResult(_accounts.Values.Where(a => !a.IsArchived).OrderBy(a => a.Name).ToList());
    }

    public Task<Account?> GetAccountByIdAsync(string id)
    {
        _accounts.TryGetValue(id, out var acc);
        return Task.FromResult(acc);
    }

    public async Task UpsertAccountAsync(Account account, bool markForSync = true)
    {
        account.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _accounts[account.Id] = account;
        await SaveDictionaryAsync("accounts.json", _accounts);

        if (markForSync)
        {
            EnqueueSync("ACCOUNT", account.Id, "UPSERT");
        }
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteAccountAsync(string id, bool markForSync = true)
    {
        if (_accounts.TryRemove(id, out _))
        {
            await SaveDictionaryAsync("accounts.json", _accounts);
            if (markForSync)
            {
                EnqueueSync("ACCOUNT", id, "DELETE");
            }
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    #endregion

    #region Transactions & Double-Entry Consistency
    public Task<List<Transaction>> GetTransactionsAsync()
    {
        var list = _transactions.Values.OrderByDescending(t => t.Timestamp).ToList();
        foreach (var tx in list)
        {
            EnrichTransaction(tx);
        }
        return Task.FromResult(list);
    }

    public Task<Transaction?> GetTransactionByIdAsync(string id)
    {
        if (_transactions.TryGetValue(id, out var tx))
        {
            EnrichTransaction(tx);
            return Task.FromResult<Transaction?>(tx);
        }
        return Task.FromResult<Transaction?>(null);
    }

    private void EnrichTransaction(Transaction tx)
    {
        if (_categories.TryGetValue(tx.CategoryId, out var cat))
        {
            tx.CategoryName = cat.Name;
            tx.CategoryIcon = cat.Icon;
            tx.CategoryColorHex = cat.ColorHex;
        }
        if (_accounts.TryGetValue(tx.SourceAccountId, out var srcAcc))
        {
            tx.SourceAccountName = srcAcc.Name;
        }
        if (!string.IsNullOrEmpty(tx.DestinationAccountId) && _accounts.TryGetValue(tx.DestinationAccountId, out var dstAcc))
        {
            tx.DestinationAccountName = dstAcc.Name;
        }
    }

    public async Task UpsertTransactionAsync(Transaction transaction, bool markForSync = true)
    {
        // Double-entry balance adjustment
        if (_transactions.TryGetValue(transaction.Id, out var existing))
        {
            // Reverse existing transaction effect
            ReverseBalanceEffect(existing);
        }

        // Apply new transaction balance effect
        ApplyBalanceEffect(transaction);

        _transactions[transaction.Id] = transaction;
        await SaveDictionaryAsync("transactions.json", _transactions);
        await SaveDictionaryAsync("accounts.json", _accounts);

        if (markForSync)
        {
            EnqueueSync("TRANSACTION", transaction.Id, "UPSERT");
        }
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteTransactionAsync(string id, bool markForSync = true)
    {
        if (_transactions.TryRemove(id, out var tx))
        {
            ReverseBalanceEffect(tx);
            await SaveDictionaryAsync("transactions.json", _transactions);
            await SaveDictionaryAsync("accounts.json", _accounts);

            if (markForSync)
            {
                EnqueueSync("TRANSACTION", id, "DELETE");
            }
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ApplyBalanceEffect(Transaction tx)
    {
        if (_accounts.TryGetValue(tx.SourceAccountId, out var srcAcc))
        {
            EnsureAvailableBalanceCurrency(srcAcc);
            switch (tx.Type)
            {
                case TransactionType.INCOME or TransactionType.REFUND:
                    srcAcc.Balance += tx.Amount;
                    srcAcc.AvailableBalance += tx.Amount;
                    break;
                case TransactionType.EXPENSE:
                    srcAcc.Balance -= tx.Amount;
                    srcAcc.AvailableBalance -= tx.Amount;
                    break;
                case TransactionType.TRANSFER:
                    srcAcc.Balance -= tx.Amount;
                    srcAcc.AvailableBalance -= tx.Amount;
                    if (!string.IsNullOrEmpty(tx.DestinationAccountId) && _accounts.TryGetValue(tx.DestinationAccountId, out var dstAcc))
                    {
                        EnsureAvailableBalanceCurrency(dstAcc);
                        var dstAmount = tx.DestinationAmount ?? tx.Amount;
                        dstAcc.Balance += dstAmount;
                        dstAcc.AvailableBalance += dstAmount;
                    }
                    break;
            }
        }
    }

    private static void EnsureAvailableBalanceCurrency(Account acc)
    {
        if (acc.AvailableBalance.CurrencyCode != acc.Balance.CurrencyCode)
        {
            acc.AvailableBalance = new Money(acc.AvailableBalance.AmountMinor, acc.Balance.CurrencyCode);
        }
    }

    private void ReverseBalanceEffect(Transaction tx)
    {
        if (_accounts.TryGetValue(tx.SourceAccountId, out var srcAcc))
        {
            switch (tx.Type)
            {
                case TransactionType.INCOME or TransactionType.REFUND:
                    srcAcc.Balance -= tx.Amount;
                    srcAcc.AvailableBalance -= tx.Amount;
                    break;
                case TransactionType.EXPENSE:
                    srcAcc.Balance += tx.Amount;
                    srcAcc.AvailableBalance += tx.Amount;
                    break;
                case TransactionType.TRANSFER:
                    srcAcc.Balance += tx.Amount;
                    srcAcc.AvailableBalance += tx.Amount;
                    if (!string.IsNullOrEmpty(tx.DestinationAccountId) && _accounts.TryGetValue(tx.DestinationAccountId, out var dstAcc))
                    {
                        var dstAmount = tx.DestinationAmount ?? tx.Amount;
                        dstAcc.Balance -= dstAmount;
                        dstAcc.AvailableBalance -= dstAmount;
                    }
                    break;
            }
        }
    }
    #endregion

    #region Categories
    public Task<List<Category>> GetCategoriesAsync()
    {
        return Task.FromResult(_categories.Values.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToList());
    }

    public Task<Category?> GetCategoryByIdAsync(string id)
    {
        _categories.TryGetValue(id, out var cat);
        return Task.FromResult(cat);
    }

    public async Task UpsertCategoryAsync(Category category, bool markForSync = true)
    {
        _categories[category.Id] = category;
        await SaveDictionaryAsync("categories.json", _categories);

        if (markForSync)
        {
            EnqueueSync("CATEGORY", category.Id, "UPSERT");
        }
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteCategoryAsync(string id, bool markForSync = true)
    {
        if (_categories.TryRemove(id, out _))
        {
            await SaveDictionaryAsync("categories.json", _categories);
            if (markForSync)
            {
                EnqueueSync("CATEGORY", id, "DELETE");
            }
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    #endregion

    #region Budgets
    public Task<List<Budget>> GetBudgetsAsync()
    {
        var list = _budgets.Values.Where(b => !b.IsArchived).OrderBy(b => b.Name).ToList();
        // Recalculate spent for current month
        var now = DateTimeOffset.UtcNow;
        long startOfMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        foreach (var b in list)
        {
            if (_categories.TryGetValue(b.CategoryId, out var cat))
                b.CategoryName = cat.Name;

            long spentMinor = _transactions.Values
                .Where(t => t.CategoryId == b.CategoryId && t.Type == TransactionType.EXPENSE && t.Timestamp >= startOfMonth && !t.IsExcludedFromBudget)
                .Sum(t => t.Amount.AmountMinor);

            if (spentMinor > 0)
                b.SpentAmount = new Money(spentMinor, b.LimitAmount.CurrencyCode);
        }

        return Task.FromResult(list);
    }

    public Task<Budget?> GetBudgetByIdAsync(string id)
    {
        _budgets.TryGetValue(id, out var b);
        return Task.FromResult(b);
    }

    public async Task UpsertBudgetAsync(Budget budget, bool markForSync = true)
    {
        _budgets[budget.Id] = budget;
        await SaveDictionaryAsync("budgets.json", _budgets);

        if (markForSync)
        {
            EnqueueSync("BUDGET", budget.Id, "UPSERT");
        }
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteBudgetAsync(string id, bool markForSync = true)
    {
        if (_budgets.TryRemove(id, out _))
        {
            await SaveDictionaryAsync("budgets.json", _budgets);
            if (markForSync)
            {
                EnqueueSync("BUDGET", id, "DELETE");
            }
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    #endregion

    #region Recurring
    public Task<List<RecurringTransaction>> GetRecurringRulesAsync()
    {
        var list = _recurringRules.Values.OrderBy(r => r.NextDueDate).ToList();
        foreach (var r in list)
        {
            if (_categories.TryGetValue(r.CategoryId, out var cat))
                r.CategoryName = cat.Name;
            if (_accounts.TryGetValue(r.AccountId, out var acc))
                r.AccountName = acc.Name;
        }
        return Task.FromResult(list);
    }

    public Task<RecurringTransaction?> GetRecurringRuleByIdAsync(string id)
    {
        _recurringRules.TryGetValue(id, out var r);
        return Task.FromResult(r);
    }

    public async Task UpsertRecurringRuleAsync(RecurringTransaction rule, bool markForSync = true)
    {
        _recurringRules[rule.Id] = rule;
        await SaveDictionaryAsync("recurring.json", _recurringRules);

        if (markForSync)
        {
            EnqueueSync("RECURRING_RULE", rule.Id, "UPSERT");
        }
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteRecurringRuleAsync(string id, bool markForSync = true)
    {
        if (_recurringRules.TryRemove(id, out _))
        {
            await SaveDictionaryAsync("recurring.json", _recurringRules);
            if (markForSync)
            {
                EnqueueSync("RECURRING_RULE", id, "DELETE");
            }
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    #endregion

    #region Goals
    public Task<List<FinancialGoal>> GetGoalsAsync()
    {
        return Task.FromResult(_goals.Values.OrderBy(g => g.TargetDate).ToList());
    }

    public Task<FinancialGoal?> GetGoalByIdAsync(string id)
    {
        _goals.TryGetValue(id, out var g);
        return Task.FromResult(g);
    }

    public async Task UpsertGoalAsync(FinancialGoal goal, bool markForSync = true)
    {
        _goals[goal.Id] = goal;
        await SaveDictionaryAsync("goals.json", _goals);

        if (markForSync)
        {
            EnqueueSync("GOAL", goal.Id, "UPSERT");
        }
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteGoalAsync(string id, bool markForSync = true)
    {
        if (_goals.TryRemove(id, out _))
        {
            await SaveDictionaryAsync("goals.json", _goals);
            if (markForSync)
            {
                EnqueueSync("GOAL", id, "DELETE");
            }
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    #endregion

    #region Settings
    public Task<CloudSettings> GetSettingsAsync() => Task.FromResult(_settings);

    public async Task SaveSettingsAsync(CloudSettings settings, bool markForSync = true)
    {
        settings.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _settings = settings;
        await SaveToFileAsync("settings.json", JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));

        if (markForSync)
        {
            EnqueueSync("SETTINGS", CloudSettings.SettingsDocumentId, "UPSERT");
        }
        DataChanged?.Invoke(this, EventArgs.Empty);
    }
    #endregion

    #region Sync Queue
    private void EnqueueSync(string type, string id, string action)
    {
        string key = $"{type}:{id}";
        _syncQueue[key] = new SyncQueueItem(type, id, action, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        _ = SaveDictionaryAsync("sync_queue.json", _syncQueue);
    }

    public Task<List<SyncQueueItem>> GetPendingSyncItemsAsync()
    {
        return Task.FromResult(_syncQueue.Values.ToList());
    }

    public async Task RemoveFromSyncQueueAsync(string entityType, string entityId)
    {
        string key = $"{entityType}:{entityId}";
        if (_syncQueue.TryRemove(key, out _))
        {
            await SaveDictionaryAsync("sync_queue.json", _syncQueue);
        }
    }

    public async Task ClearAllDataAsync()
    {
        _accounts.Clear();
        _transactions.Clear();
        _categories.Clear();
        _budgets.Clear();
        _recurringRules.Clear();
        _goals.Clear();
        _syncQueue.Clear();

        await SaveDictionaryAsync("accounts.json", _accounts);
        await SaveDictionaryAsync("transactions.json", _transactions);
        await SaveDictionaryAsync("categories.json", _categories);
        await SaveDictionaryAsync("budgets.json", _budgets);
        await SaveDictionaryAsync("recurring.json", _recurringRules);
        await SaveDictionaryAsync("goals.json", _goals);
        await SaveDictionaryAsync("sync_queue.json", _syncQueue);

        DataChanged?.Invoke(this, EventArgs.Empty);
    }
    #endregion

    #region File I/O Helpers
    private async Task SaveDictionaryAsync<TKey, TValue>(string filename, ConcurrentDictionary<TKey, TValue> dict) where TKey : notnull
    {
        try
        {
            string path = Path.Combine(_dataDir, filename);
            string json = JsonSerializer.Serialize(dict.Values, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(path, json);
        }
        catch { }
    }

    private async Task LoadDictionaryAsync<T>(string filename, ConcurrentDictionary<string, T> dict) where T : class
    {
        try
        {
            string path = Path.Combine(_dataDir, filename);
            if (File.Exists(path))
            {
                string json = await File.ReadAllTextAsync(path);
                var list = JsonSerializer.Deserialize<List<T>>(json);
                if (list != null)
                {
                    dict.Clear();
                    foreach (var item in list)
                    {
                        var prop = typeof(T).GetProperty("Id");
                        if (prop != null)
                        {
                            string? id = prop.GetValue(item)?.ToString();
                            if (!string.IsNullOrEmpty(id))
                                dict[id] = item;
                        }
                    }
                }
            }
        }
        catch { }
    }

    private async Task LoadDictionaryAsync(string filename, ConcurrentDictionary<string, SyncQueueItem> dict)
    {
        try
        {
            string path = Path.Combine(_dataDir, filename);
            if (File.Exists(path))
            {
                string json = await File.ReadAllTextAsync(path);
                var list = JsonSerializer.Deserialize<List<SyncQueueItem>>(json);
                if (list != null)
                {
                    dict.Clear();
                    foreach (var item in list)
                    {
                        dict[$"{item.EntityType}:{item.EntityId}"] = item;
                    }
                }
            }
        }
        catch { }
    }

    private async Task SaveToFileAsync(string filename, string content)
    {
        try
        {
            string path = Path.Combine(_dataDir, filename);
            await File.WriteAllTextAsync(path, content);
        }
        catch { }
    }

    private async Task LoadFromFileAsync(string filename, Action<string> action)
    {
        try
        {
            string path = Path.Combine(_dataDir, filename);
            if (File.Exists(path))
            {
                string content = await File.ReadAllTextAsync(path);
                action(content);
            }
        }
        catch { }
    }
    #endregion
}
