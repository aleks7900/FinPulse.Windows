using System.Collections.Generic;
using System.Threading.Tasks;
using FinPulse.Windows.Models;

namespace FinPulse.Windows.Repositories;

public interface IAccountRepository
{
    Task<List<Account>> GetAllAsync();
    Task<Account?> GetByIdAsync(string id);
    Task SaveAsync(Account account);
    Task DeleteAsync(string id);
    Task TransferAsync(string sourceId, string destinationId, Money amount, string? note = null);
}

public interface ITransactionRepository
{
    Task<List<Transaction>> GetAllAsync();
    Task<Transaction?> GetByIdAsync(string id);
    Task SaveAsync(Transaction transaction);
    Task DeleteAsync(string id);
}

public interface ICategoryRepository
{
    Task<List<Category>> GetAllAsync();
    Task<Category?> GetByIdAsync(string id);
    Task SaveAsync(Category category);
    Task DeleteAsync(string id);
}

public interface IBudgetRepository
{
    Task<List<Budget>> GetAllAsync();
    Task<Budget?> GetByIdAsync(string id);
    Task SaveAsync(Budget budget);
    Task DeleteAsync(string id);
}

public interface IGoalRepository
{
    Task<List<FinancialGoal>> GetAllAsync();
    Task<FinancialGoal?> GetByIdAsync(string id);
    Task SaveAsync(FinancialGoal goal);
    Task DeleteAsync(string id);
    Task ContributeAsync(string goalId, Money amount);
}

public interface IRecurringRepository
{
    Task<List<RecurringTransaction>> GetAllAsync();
    Task<RecurringTransaction?> GetByIdAsync(string id);
    Task SaveAsync(RecurringTransaction rule);
    Task DeleteAsync(string id);
}
