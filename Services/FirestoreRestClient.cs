using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FinPulse.Windows.Models;

namespace FinPulse.Windows.Services;

public class FirestoreRestClient : IFirestoreClient
{
    private const string ProjectId = "finpulse-cloud";
    private const string BaseFirestoreUrl = $"https://firestore.googleapis.com/v1/projects/{ProjectId}/databases/(default)/documents";

    private readonly HttpClient _httpClient;
    private readonly IAuthService _authService;

    public FirestoreRestClient(IAuthService authService, HttpClient? httpClient = null)
    {
        _authService = authService;
        _httpClient = httpClient ?? new HttpClient();
    }

    private async Task<HttpRequestMessage> CreateAuthorizedRequestAsync(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        string? token = await _authService.GetValidTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return request;
    }

    public async Task<int> UploadRecordsAsync(string uid, string collection, List<CloudEntityRecord> records)
    {
        if (records == null || records.Count == 0) return 0;

        int count = 0;
        foreach (var record in records)
        {
            string url = $"{BaseFirestoreUrl}/users/{uid}/{collection}/{record.Id}";
            var fields = new Dictionary<string, object>
            {
                ["id"] = new { stringValue = record.Id },
                ["collection"] = new { stringValue = record.Collection },
                ["jsonPayload"] = new { stringValue = record.JsonPayload },
                ["updatedAt"] = new { integerValue = record.UpdatedAt.ToString() },
                ["isDeleted"] = new { booleanValue = record.IsDeleted }
            };

            if (record.DeletedAt.HasValue)
            {
                fields["deletedAt"] = new { integerValue = record.DeletedAt.Value.ToString() };
            }

            var body = new { fields };
            string jsonBody = JsonSerializer.Serialize(body);

            using var request = await CreateAuthorizedRequestAsync(HttpMethod.Patch, url);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                count++;
            }
        }

        // Touch root user document with lastModifiedTimestamp
        try
        {
            string rootUrl = $"{BaseFirestoreUrl}/users/{uid}";
            var rootFields = new Dictionary<string, object>
            {
                ["uid"] = new { stringValue = uid },
                ["lastModifiedTimestamp"] = new { integerValue = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString() }
            };
            using var rootReq = await CreateAuthorizedRequestAsync(HttpMethod.Patch, rootUrl);
            rootReq.Content = new StringContent(JsonSerializer.Serialize(new { fields = rootFields }), Encoding.UTF8, "application/json");
            await _httpClient.SendAsync(rootReq);
        }
        catch { }

        return count;
    }

    public async Task<List<CloudEntityRecord>> DownloadRecordsAsync(string uid, string collection, long sinceTimestamp)
    {
        var records = new List<CloudEntityRecord>();
        string url = $"{BaseFirestoreUrl}/users/{uid}/{collection}?pageSize=300";

        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, url);
        using var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            return records;
        }

        string content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        if (!doc.RootElement.TryGetProperty("documents", out var docsArray) || docsArray.ValueKind != JsonValueKind.Array)
        {
            return records;
        }

        foreach (var docElem in docsArray.EnumerateArray())
        {
            if (!docElem.TryGetProperty("fields", out var fields)) continue;

            string id = fields.TryGetProperty("id", out var idProp) && idProp.TryGetProperty("stringValue", out var idVal)
                ? idVal.GetString() ?? ""
                : "";

            if (string.IsNullOrWhiteSpace(id))
            {
                // Fallback to document path ending
                string name = docElem.GetProperty("name").GetString() ?? "";
                int lastSlash = name.LastIndexOf('/');
                id = lastSlash >= 0 ? name[(lastSlash + 1)..] : name;
            }

            string col = fields.TryGetProperty("collection", out var colProp) && colProp.TryGetProperty("stringValue", out var colVal)
                ? colVal.GetString() ?? collection
                : collection;

            string jsonPayload = fields.TryGetProperty("jsonPayload", out var jpProp) && jpProp.TryGetProperty("stringValue", out var jpVal)
                ? jpVal.GetString() ?? ""
                : "";

            long updatedAt = 0;
            if (fields.TryGetProperty("updatedAt", out var uaProp) && uaProp.TryGetProperty("integerValue", out var uaVal))
            {
                long.TryParse(uaVal.GetString(), out updatedAt);
            }

            bool isDeleted = fields.TryGetProperty("isDeleted", out var isDelProp) && isDelProp.TryGetProperty("booleanValue", out var isDelVal) && isDelVal.GetBoolean();

            long? deletedAt = null;
            if (fields.TryGetProperty("deletedAt", out var daProp) && daProp.TryGetProperty("integerValue", out var daVal))
            {
                if (long.TryParse(daVal.GetString(), out long dVal))
                    deletedAt = dVal;
            }

            // Filter by sinceTimestamp if requested
            if (sinceTimestamp > 0 && updatedAt <= sinceTimestamp)
            {
                continue;
            }

            records.Add(new CloudEntityRecord
            {
                Id = id,
                Collection = col,
                JsonPayload = jsonPayload,
                UpdatedAt = updatedAt,
                IsDeleted = isDeleted,
                DeletedAt = deletedAt
            });
        }

        return records;
    }

    public async Task RecordTombstoneAsync(string uid, string collection, string id, long deletedAt)
    {
        string url = $"{BaseFirestoreUrl}/users/{uid}/{collection}/{id}";
        var fields = new Dictionary<string, object>
        {
            ["id"] = new { stringValue = id },
            ["collection"] = new { stringValue = collection },
            ["jsonPayload"] = new { stringValue = "" },
            ["updatedAt"] = new { integerValue = deletedAt.ToString() },
            ["isDeleted"] = new { booleanValue = true },
            ["deletedAt"] = new { integerValue = deletedAt.ToString() }
        };

        var body = new { fields };
        string jsonBody = JsonSerializer.Serialize(body);

        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Patch, url);
        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        await _httpClient.SendAsync(request);
    }

    public async Task<CloudUserSummary> GetUserSummaryAsync(string uid)
    {
        string rootUrl = $"{BaseFirestoreUrl}/users/{uid}";
        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, rootUrl);
        using var response = await _httpClient.SendAsync(request);

        var summary = new CloudUserSummary
        {
            TotalRecords = 0,
            LastModifiedTimestamp = 0
        };

        if (response.IsSuccessStatusCode)
        {
            string content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("fields", out var fields))
            {
                if (fields.TryGetProperty("lastModifiedTimestamp", out var lmt) && lmt.TryGetProperty("integerValue", out var lmtVal))
                {
                    if (long.TryParse(lmtVal.GetString(), out long lastMod))
                        summary.LastModifiedTimestamp = lastMod;
                }
                if (fields.TryGetProperty("totalRecords", out var tr) && tr.TryGetProperty("integerValue", out var trVal))
                {
                    if (int.TryParse(trVal.GetString(), out int tot))
                        summary.TotalRecords = tot;
                }
            }
        }

        return summary;
    }

    public async Task ClearUserStorageAsync(string uid)
    {
        string[] collections = {
            "transactions", "accounts", "categories", "budgets",
            "recurring_rules", "goals", "assets", "debts",
            "categorization_rules", "saved_filters", "settings"
        };

        foreach (var col in collections)
        {
            var records = await DownloadRecordsAsync(uid, col, 0);
            foreach (var rec in records)
            {
                string url = $"{BaseFirestoreUrl}/users/{uid}/{col}/{rec.Id}";
                using var delReq = await CreateAuthorizedRequestAsync(HttpMethod.Delete, url);
                await _httpClient.SendAsync(delReq);
            }
        }

        string rootUrl = $"{BaseFirestoreUrl}/users/{uid}";
        using var rootDelReq = await CreateAuthorizedRequestAsync(HttpMethod.Delete, rootUrl);
        await _httpClient.SendAsync(rootDelReq);
    }
}
