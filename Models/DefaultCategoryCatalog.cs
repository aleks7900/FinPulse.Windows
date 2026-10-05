using System.Collections.Generic;

namespace FinPulse.Windows.Models;

public static class DefaultCategoryCatalog
{
    public static List<Category> GetDefaultCategories()
    {
        int order = 1;
        var list = new List<Category>
        {
            // EXPENSE CATEGORIES
            // 1. Home & Utilities
            new() { Id = "cat_housing", Name = "Home & Utilities", Type = CategoryType.EXPENSE, Icon = "home", ColorHex = 0xFF3F51B5, IsDefault = true, SortOrder = order++ },
            new() { Id = "cat_home_rent", Name = "Rent", Type = CategoryType.EXPENSE, ParentCategoryId = "cat_housing", Icon = "home", ColorHex = 0xFF3F51B5, IsDefault = true, SortOrder = order++ },
            new() { Id = "cat_home_utilities", Name = "Utilities", Type = CategoryType.EXPENSE, ParentCategoryId = "cat_housing", Icon = "utilities", ColorHex = 0xFF00ACC1, IsDefault = true, SortOrder = order++ },
            new() { Id = "cat_home_internet", Name = "Internet", Type = CategoryType.EXPENSE, ParentCategoryId = "cat_housing", Icon = "wifi", ColorHex = 0xFF00897B, IsDefault = true, SortOrder = order++ },
            new() { Id = "cat_home_phone", Name = "Mobile Phone", Type = CategoryType.EXPENSE, ParentCategoryId = "cat_housing", Icon = "phone", ColorHex = 0xFF43A047, IsDefault = true, SortOrder = order++ },

            // 2. Food & Dining
            new() { Id = "cat_food", Name = "Food & Dining", Type = CategoryType.EXPENSE, Icon = "fastfood", ColorHex = 0xFFFF9800, IsDefault = true, SortOrder = order++ },
            new() { Id = "cat_groceries", Name = "Groceries", Type = CategoryType.EXPENSE, ParentCategoryId = "cat_food", Icon = "shoppingcart", ColorHex = 0xFF4CAF50, IsDefault = true, SortOrder = order++ },
            new() { Id = "cat_food_restaurants", Name = "Restaurants", Type = CategoryType.EXPENSE, ParentCategoryId = "cat_food", Icon = "restaurant", ColorHex = 0xFFFF9800, IsDefault = true, SortOrder = order++ },
            new() { Id = "cat_food_cafes", Name = "Cafes & Coffee", Type = CategoryType.EXPENSE, ParentCategoryId = "cat_food", Icon = "cafe", ColorHex = 0xFF795548, IsDefault = true, SortOrder = order++ },
            new() { Id = "cat_food_delivery", Name = "Food Delivery", Type = CategoryType.EXPENSE, ParentCategoryId = "cat_food", Icon = "delivery", ColorHex = 0xFFFF5722, IsDefault = true, SortOrder = order++ },

            // 3. Transportation
            new() { Id = "cat_transport", Name = "Transportation", Type = CategoryType.EXPENSE, Icon = "transport", ColorHex = 0xFF2196F3, IsDefault = true, SortOrder = order++ },
            new() { Id = "cat_transport_public", Name = "Public Transport", Type = CategoryType.EXPENSE, ParentCategoryId = "cat_transport", Icon = "directionsbus", ColorHex = 0xFF1E88E5, IsDefault = true, SortOrder = order++ },
            new() { Id = "cat_transport_taxi", Name = "Taxi & Rideshare", Type = CategoryType.EXPENSE, ParentCategoryId = "cat_transport", Icon = "taxi", ColorHex = 0xFFFDD835, IsDefault = true, SortOrder = order++ },
            new() { Id = "cat_fuel", Name = "Fuel & Gas", Type = CategoryType.EXPENSE, ParentCategoryId = "cat_transport", Icon = "fuel", ColorHex = 0xFF00BCD4, IsDefault = true, SortOrder = order++ },
            new() { Id = "cat_transport_maintenance", Name = "Car Maintenance", Type = CategoryType.EXPENSE, ParentCategoryId = "cat_transport", Icon = "build", ColorHex = 0xFF78909C, IsDefault = true, SortOrder = order++ },

            // 4. Shopping
            new() { Id = "cat_shopping", Name = "Shopping", Type = CategoryType.EXPENSE, Icon = "shopping", ColorHex = 0xFFE91E63, IsDefault = true, SortOrder = order++ },
            new() { Id = "cat_shopping_clothing", Name = "Clothing", Type = CategoryType.EXPENSE, ParentCategoryId = "cat_shopping", Icon = "checkroom", ColorHex = 0xFFEC407A, IsDefault = true, SortOrder = order++ },
            new() { Id = "cat_shopping_electronics", Name = "Electronics", Type = CategoryType.EXPENSE, ParentCategoryId = "cat_shopping", Icon = "devices", ColorHex = 0xFF5C6BC0, IsDefault = true, SortOrder = order++ },
            new() { Id = "cat_shopping_online", Name = "Online Shopping", Type = CategoryType.EXPENSE, ParentCategoryId = "cat_shopping", Icon = "shoppingcart", ColorHex = 0xFFE91E63, IsDefault = true, SortOrder = order++ },

            // 5. Health & Fitness
            new() { Id = "cat_health", Name = "Health & Medical", Type = CategoryType.EXPENSE, Icon = "health", ColorHex = 0xFFF44336, IsDefault = true, SortOrder = order++ },
            new() { Id = "cat_health_pharmacy", Name = "Pharmacy", Type = CategoryType.EXPENSE, ParentCategoryId = "cat_health", Icon = "medication", ColorHex = 0xFFE53935, IsDefault = true, SortOrder = order++ },
            new() { Id = "cat_fitness", Name = "Fitness & Sports", Type = CategoryType.EXPENSE, ParentCategoryId = "cat_health", Icon = "fitness", ColorHex = 0xFF009688, IsDefault = true, SortOrder = order++ },

            // 6. Subscriptions & Entertainment
            new() { Id = "cat_entertainment", Name = "Entertainment & Media", Type = CategoryType.EXPENSE, Icon = "tv", ColorHex = 0xFF9C27B0, IsDefault = true, SortOrder = order++ },
            new() { Id = "cat_subscriptions", Name = "Subscriptions", Type = CategoryType.EXPENSE, ParentCategoryId = "cat_entertainment", Icon = "tv", ColorHex = 0xFF673AB7, IsDefault = true, SortOrder = order++ },

            // INCOME CATEGORIES
            new() { Id = "cat_income_salary", Name = "Salary & Wages", Type = CategoryType.INCOME, Icon = "salary", ColorHex = 0xFF2E7D32, IsDefault = true, SortOrder = order++ },
            new() { Id = "cat_income_freelance", Name = "Freelance & Consulting", Type = CategoryType.INCOME, Icon = "work", ColorHex = 0xFF43A047, IsDefault = true, SortOrder = order++ },
            new() { Id = "cat_income_investments", Name = "Investments & Dividends", Type = CategoryType.INCOME, Icon = "accountbalance", ColorHex = 0xFF00897B, IsDefault = true, SortOrder = order++ },
            new() { Id = "cat_income_gifts", Name = "Gifts & Grants", Type = CategoryType.INCOME, Icon = "gifts", ColorHex = 0xFF8E24AA, IsDefault = true, SortOrder = order++ },
            new() { Id = "cat_income_other", Name = "Other Income", Type = CategoryType.INCOME, Icon = "payment", ColorHex = 0xFF546E7A, IsDefault = true, SortOrder = order++ },
        };

        return list;
    }
}
