using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FinPulse.Windows.Models;

namespace FinPulse.Windows.Services;

public record SyncQueueItem(
    string EntityType,
    string EntityId,
    string Action, // "UPSERT" or "DELETE"
    long Timestamp
);

public interface ILocalDataStore
{
    event EventHandler? DataChanged;

    Task InitializeAsync();

    // Accounts
    Task<List<Account>> GetAccountsAsync();
    Task<Account?> GetAccountByIdAsync(string id);
    Task UpsertAccountAsync(Account account, bool markForSync = true);
    Task DeleteAccountAsync(string id, bool markForSync = true);

    // Transactions
    Task<List<Transaction>> GetTransactionsAsync();
    Task<Transaction?> GetTransactionByIdAsync(string id);
    Task UpsertTransactionAsync(Transaction transaction, bool markForSync = true);
    Task DeleteTransactionAsync(string id, bool markForSync = true);

    // Categories
    Task<List<Category>> GetCategoriesAsync();
    Task<Category?> GetCategoryByIdAsync(string id);
    Task UpsertCategoryAsync(Category category, bool markForSync = true);
    Task DeleteCategoryAsync(string id, bool markForSync = true);

    // Budgets
    Task<List<Budget>> GetBudgetsAsync();
    Task<Budget?> GetBudgetByIdAsync(string id);
    Task UpsertBudgetAsync(Budget budget, bool markForSync = true);
    Task DeleteBudgetAsync(string id, bool markForSync = true);

    // Recurring
    Task<List<RecurringTransaction>> GetRecurringRulesAsync();
    Task<RecurringTransaction?> GetRecurringRuleByIdAsync(string id);
    Task UpsertRecurringRuleAsync(RecurringTransaction rule, bool markForSync = true);
    Task DeleteRecurringRuleAsync(string id, bool markForSync = true);

    // Goals
    Task<List<FinancialGoal>> GetGoalsAsync();
    Task<FinancialGoal?> GetGoalByIdAsync(string id);
    Task UpsertGoalAsync(FinancialGoal goal, bool markForSync = true);
    Task DeleteGoalAsync(string id, bool markForSync = true);

    // Settings
    Task<CloudSettings> GetSettingsAsync();
    Task SaveSettingsAsync(CloudSettings settings, bool markForSync = true);

    // Sync queue
    Task<List<SyncQueueItem>> GetPendingSyncItemsAsync();
    Task RemoveFromSyncQueueAsync(string entityType, string entityId);
    Task ClearAllDataAsync();
}
