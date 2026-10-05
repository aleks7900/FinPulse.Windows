using System;
using System.Text.Json.Serialization;

namespace FinPulse.Windows.Models;

public class CloudEntityRecord
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("collection")]
    public string Collection { get; set; } = string.Empty;

    [JsonPropertyName("jsonPayload")]
    public string JsonPayload { get; set; } = string.Empty;

    [JsonPropertyName("updatedAt")]
    public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; } = false;

    [JsonPropertyName("deletedAt")]
    public long? DeletedAt { get; set; }
}

public class CloudUserSummary
{
    public int TotalRecords { get; set; }
    public long LastModifiedTimestamp { get; set; }
}

public class SyncResult
{
    public bool IsSuccess { get; set; }
    public int UploadedCount { get; set; }
    public int DownloadedCount { get; set; }
    public int DeletedCount { get; set; }
    public int ConflictsResolvedCount { get; set; }
    public long SyncedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
