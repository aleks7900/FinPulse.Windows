using System;
using System.Text.Json.Serialization;

namespace FinPulse.Windows.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CategoryType
{
    EXPENSE,
    INCOME
}

public class Category
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public CategoryType Type { get; set; } = CategoryType.EXPENSE;

    [JsonPropertyName("parentCategoryId")]
    public string? ParentCategoryId { get; set; }

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "category";

    [JsonPropertyName("colorHex")]
    public long ColorHex { get; set; } = 0xFF607D8B;

    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; } = false;

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; } = 0;

    [JsonIgnore]
    public string HexColorString => $"#{((uint)ColorHex):X8}";

    [JsonIgnore]
    public string GlyphIcon => Icon switch
    {
        "home" => "\uE80F",
        "shoppingcart" => "\uE7BF",
        "restaurant" or "fastfood" => "\uE7EE",
        "transport" or "directionsbus" => "\uE787",
        "fuel" => "\uE7B5",
        "health" or "medication" => "\uE95E",
        "fitness" => "\uE7FC",
        "devices" => "\uE7F4",
        "cafe" => "\uE7EE",
        "wifi" => "\uE701",
        "phone" => "\uE717",
        "tv" => "\uE7F4",
        "payment" or "receipt" => "\uE8C7",
        "work" or "salary" => "\uE821",
        _ => "\uE8EC" // Tag
    };
}
