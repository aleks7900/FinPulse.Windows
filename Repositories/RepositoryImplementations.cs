using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FinPulse.Windows.Models;
using FinPulse.Windows.Services;

namespace FinPulse.Windows.Repositories;

public class AccountRepository : IAccountRepository
{
    private readonly ILocalDataStore _store;
    public AccountRepository(ILocalDataStore store) => _store = store;

    public Task<List<Account>> GetAllAsync() => _store.GetAccountsAsync();
    public Task<Account?> GetByIdAsync(string id) => _store.GetAccountByIdAsync(id);
    public Task SaveAsync(Account account) => _store.UpsertAccountAsync(account);
    public Task DeleteAsync(string id) => _store.DeleteAccountAsync(id);

    public async Task TransferAsync(string sourceId, string destinationId, Money amount, string? note = null)
    {
        var src = await _store.GetAccountByIdAsync(sourceId);
        var dst = await _store.GetAccountByIdAsync(destinationId);
        if (src == null || dst == null) throw new InvalidOperationException("Source or destination account not found");

        var transferTx = new Transaction
        {
            Id = Guid.NewGuid().ToString(),
            Amount = amount,
            Type = TransactionType.TRANSFER,
            SourceAccountId = sourceId,
            DestinationAccountId = destinationId,
            CategoryId = "cat_financial_transfer",
            Description = $"Transfer: {src.Name} -> {dst.Name}",
            Notes = note,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        await _store.UpsertTransactionAsync(transferTx);
    }
}

public class TransactionRepository : ITransactionRepository
{
    private readonly ILocalDataStore _store;
    public TransactionRepository(ILocalDataStore store) => _store = store;

    public Task<List<Transaction>> GetAllAsync() => _store.GetTransactionsAsync();
    public Task<Transaction?> GetByIdAsync(string id) => _store.GetTransactionByIdAsync(id);
    public Task SaveAsync(Transaction transaction) => _store.UpsertTransactionAsync(transaction);
    public Task DeleteAsync(string id) => _store.DeleteTransactionAsync(id);
}

public class CategoryRepository : ICategoryRepository
{
    private readonly ILocalDataStore _store;
    public CategoryRepository(ILocalDataStore store) => _store = store;

    public Task<List<Category>> GetAllAsync() => _store.GetCategoriesAsync();
    public Task<Category?> GetByIdAsync(string id) => _store.GetCategoryByIdAsync(id);
    public Task SaveAsync(Category category) => _store.UpsertCategoryAsync(category);
    public Task DeleteAsync(string id) => _store.DeleteCategoryAsync(id);
}

public class BudgetRepository : IBudgetRepository
{
    private readonly ILocalDataStore _store;
    public BudgetRepository(ILocalDataStore store) => _store = store;

    public Task<List<Budget>> GetAllAsync() => _store.GetBudgetsAsync();
    public Task<Budget?> GetByIdAsync(string id) => _store.GetBudgetByIdAsync(id);
    public Task SaveAsync(Budget budget) => _store.UpsertBudgetAsync(budget);
    public Task DeleteAsync(string id) => _store.DeleteBudgetAsync(id);
}

public class GoalRepository : IGoalRepository
{
    private readonly ILocalDataStore _store;
    public GoalRepository(ILocalDataStore store) => _store = store;

    public Task<List<FinancialGoal>> GetAllAsync() => _store.GetGoalsAsync();
    public Task<FinancialGoal?> GetByIdAsync(string id) => _store.GetGoalByIdAsync(id);
    public Task SaveAsync(FinancialGoal goal) => _store.UpsertGoalAsync(goal);
    public Task DeleteAsync(string id) => _store.DeleteGoalAsync(id);

    public async Task ContributeAsync(string goalId, Money amount)
    {
        var goal = await _store.GetGoalByIdAsync(goalId);
        if (goal == null) return;

        goal.CurrentAmount += amount;
        if (goal.CurrentAmount >= goal.TargetAmount)
        {
            goal.IsCompleted = true;
        }
        await _store.UpsertGoalAsync(goal);
    }
}

public class RecurringRepository : IRecurringRepository
{
    private readonly ILocalDataStore _store;
    public RecurringRepository(ILocalDataStore store) => _store = store;

    public Task<List<RecurringTransaction>> GetAllAsync() => _store.GetRecurringRulesAsync();
    public Task<RecurringTransaction?> GetByIdAsync(string id) => _store.GetRecurringRuleByIdAsync(id);
    public Task SaveAsync(RecurringTransaction rule) => _store.UpsertRecurringRuleAsync(rule);
    public Task DeleteAsync(string id) => _store.DeleteRecurringRuleAsync(id);
}
