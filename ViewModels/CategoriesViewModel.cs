using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using FinPulse.Windows.Models;
using FinPulse.Windows.Repositories;
using FinPulse.Windows.Services;

namespace FinPulse.Windows.ViewModels;

public class CategoriesViewModel : ViewModelBase
{
    private readonly ICategoryRepository _categoryRepo;
    private readonly ILocalDataStore _store;

    public ObservableCollection<Category> ExpenseCategories { get; } = new();
    public ObservableCollection<Category> IncomeCategories { get; } = new();

    public IAsyncRelayCommand LoadCategoriesCommand { get; }

    public CategoriesViewModel(ICategoryRepository categoryRepo, ILocalDataStore store)
    {
        _categoryRepo = categoryRepo;
        _store = store;

        LoadCategoriesCommand = new AsyncRelayCommand(LoadCategoriesAsync);
        _store.DataChanged += (s, e) => _ = LoadCategoriesAsync();
    }

    public async Task LoadCategoriesAsync()
    {
        IsBusy = true;
        try
        {
            var list = await _categoryRepo.GetAllAsync();
            ExpenseCategories.Clear();
            IncomeCategories.Clear();

            foreach (var c in list)
            {
                if (c.Type == CategoryType.EXPENSE)
                    ExpenseCategories.Add(c);
                else
                    IncomeCategories.Add(c);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task AddCategoryAsync(string name, CategoryType type, string icon, long colorHex)
    {
        var cat = new Category
        {
            Id = $"cat_custom_{Guid.NewGuid():N}",
            Name = name,
            Type = type,
            Icon = icon,
            ColorHex = colorHex,
            IsDefault = false
        };

        await _categoryRepo.SaveAsync(cat);
        await LoadCategoriesAsync();
    }

    public async Task DeleteCategoryAsync(string id)
    {
        await _categoryRepo.DeleteAsync(id);
        await LoadCategoriesAsync();
    }
}
