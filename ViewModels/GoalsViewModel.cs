using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using FinPulse.Windows.Models;
using FinPulse.Windows.Repositories;
using FinPulse.Windows.Services;

namespace FinPulse.Windows.ViewModels;

public class GoalsViewModel : ViewModelBase
{
    private readonly IGoalRepository _goalRepo;
    private readonly IAccountRepository _accountRepo;
    private readonly ILocalDataStore _store;

    public ObservableCollection<FinancialGoal> Goals { get; } = new();
    public ObservableCollection<Account> Accounts { get; } = new();

    private Money _totalTargetSavings = Money.Zero();
    public Money TotalTargetSavings
    {
        get => _totalTargetSavings;
        set => SetProperty(ref _totalTargetSavings, value);
    }

    private Money _totalCurrentSavings = Money.Zero();
    public Money TotalCurrentSavings
    {
        get => _totalCurrentSavings;
        set => SetProperty(ref _totalCurrentSavings, value);
    }

    public IAsyncRelayCommand LoadGoalsCommand { get; }

    public GoalsViewModel(IGoalRepository goalRepo, IAccountRepository accountRepo, ILocalDataStore store)
    {
        _goalRepo = goalRepo;
        _accountRepo = accountRepo;
        _store = store;

        LoadGoalsCommand = new AsyncRelayCommand(LoadGoalsAsync);
        _store.DataChanged += (s, e) => _ = LoadGoalsAsync();
    }

    public async Task LoadGoalsAsync()
    {
        IsBusy = true;
        try
        {
            var accs = await _accountRepo.GetAllAsync();
            Accounts.Clear();
            foreach (var a in accs) Accounts.Add(a);

            var list = await _goalRepo.GetAllAsync();
            Goals.Clear();
            long targetMinor = 0;
            long currentMinor = 0;
            string cur = "USD";

            foreach (var g in list)
            {
                Goals.Add(g);
                targetMinor += g.TargetAmount.AmountMinor;
                currentMinor += g.CurrentAmount.AmountMinor;
                cur = g.TargetAmount.CurrencyCode;
            }

            TotalTargetSavings = new Money(targetMinor, cur);
            TotalCurrentSavings = new Money(currentMinor, cur);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task AddGoalAsync(string title, decimal targetAmount, DateTimeOffset targetDate, string? linkedAccountId)
    {
        var settings = await _store.GetSettingsAsync();
        string cur = string.IsNullOrWhiteSpace(settings.BaseCurrencyCode) ? "EUR" : settings.BaseCurrencyCode;

        var goal = new FinancialGoal
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            TargetAmount = Money.FromMajor(targetAmount, cur),
            CurrentAmount = Money.Zero(cur),
            TargetDate = targetDate.ToUnixTimeMilliseconds(),
            LinkedAccountId = linkedAccountId,
            ColorHex = 0xFF43A047
        };

        await _goalRepo.SaveAsync(goal);
        await LoadGoalsAsync();
    }

    public async Task ContributeToGoalAsync(string goalId, decimal depositAmount)
    {
        var goal = await _goalRepo.GetByIdAsync(goalId);
        if (goal == null) return;

        var money = Money.FromMajor(depositAmount, goal.TargetAmount.CurrencyCode);
        await _goalRepo.ContributeAsync(goalId, money);
        await LoadGoalsAsync();
    }

    public async Task DeleteGoalAsync(string id)
    {
        await _goalRepo.DeleteAsync(id);
        await LoadGoalsAsync();
    }
}
