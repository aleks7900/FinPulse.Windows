using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FinPulse.Windows.Models;

namespace FinPulse.Windows.Services;

/// <summary>
/// Robust Firestore REST API client for FinPulse.
/// Implements bidirectional translation between Firestore REST format and domain models,
/// guaranteeing full interoperability with FinPulse Android and Cloud Firestore security rules.
/// </summary>
public class FirestoreRestClient : IFirestoreClient
{
    private static string BaseFirestoreUrl => FirebaseConfig.BaseFirestoreUrl;

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

        Debug.WriteLine($"[Firestore] Uploading {records.Count} records to /users/{uid}/{collection}...");
        int count = 0;

        foreach (var record in records)
        {
            string url = $"{BaseFirestoreUrl}/users/{uid}/{collection}/{record.Id}";

            // Convert jsonPayload into native Firestore fields + envelope fields for full cross-platform compatibility
            var fields = FirestoreDocumentConverter.ConvertJsonToFirestoreFields(
                record.JsonPayload,
                record.Id,
                record.Collection,
                record.UpdatedAt,
                record.IsDeleted,
                record.DeletedAt
            );

            var body = new { fields };
            string jsonBody = JsonSerializer.Serialize(body);

            try
            {
                using var request = await CreateAuthorizedRequestAsync(HttpMethod.Patch, url);
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    count++;
                }
                else
                {
                    string errContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[Firestore] ERROR PATCH /users/{uid}/{collection}/{record.Id} failed (HTTP {(int)response.StatusCode} {response.ReasonPhrase}): {errContent}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Firestore] EXCEPTION uploading record {record.Id} to {collection}: {ex.Message}");
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
            using var rootResp = await _httpClient.SendAsync(rootReq);
            if (!rootResp.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[Firestore] Note: root user touch returned HTTP {(int)rootResp.StatusCode}");
            }
        }
        catch { }

        Debug.WriteLine($"[Firestore] Successfully uploaded {count}/{records.Count} records to /users/{uid}/{collection}");
        return count;
    }

    public async Task<List<CloudEntityRecord>> DownloadRecordsAsync(string uid, string collection, long sinceTimestamp)
    {
        var records = new List<CloudEntityRecord>();
        string url = $"{BaseFirestoreUrl}/users/{uid}/{collection}?pageSize=300";

        Debug.WriteLine($"[Firestore] Fetching records from /users/{uid}/{collection} (since: {sinceTimestamp})...");

        try
        {
            using var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, url);
            using var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string errContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[Firestore] ERROR GET /users/{uid}/{collection} failed (HTTP {(int)response.StatusCode} {response.ReasonPhrase}): {errContent}");
                return records;
            }

            string content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty("documents", out var docsArray) || docsArray.ValueKind != JsonValueKind.Array)
            {
                Debug.WriteLine($"[Firestore] /users/{uid}/{collection}: No documents found or collection is empty.");
                return records;
            }

            foreach (var docElem in docsArray.EnumerateArray())
            {
                if (!docElem.TryGetProperty("fields", out var fields)) continue;

                // 1. Resolve Document ID
                string id = fields.TryGetProperty("id", out var idProp) && idProp.TryGetProperty("stringValue", out var idVal)
                    ? idVal.GetString() ?? ""
                    : "";

                if (string.IsNullOrWhiteSpace(id))
                {
                    string name = docElem.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "" : "";
                    int lastSlash = name.LastIndexOf('/');
                    id = lastSlash >= 0 ? name[(lastSlash + 1)..] : name;
                }

                if (string.IsNullOrWhiteSpace(id)) continue;

                // 2. Resolve Collection
                string col = fields.TryGetProperty("collection", out var colProp) && colProp.TryGetProperty("stringValue", out var colVal)
                    ? colVal.GetString() ?? collection
                    : collection;

                // 3. Resolve JSON Payload
                string jsonPayload = fields.TryGetProperty("jsonPayload", out var jpProp) && jpProp.TryGetProperty("stringValue", out var jpVal)
                    ? jpVal.GetString() ?? ""
                    : "";

                if (string.IsNullOrWhiteSpace(jsonPayload))
                {
                    // Native Firestore document without jsonPayload envelope -> Convert fields to JSON
                    jsonPayload = FirestoreDocumentConverter.ConvertFieldsToJson(fields, id);
                }

                // 4. Resolve UpdatedAt timestamp
                long updatedAt = 0;
                if (fields.TryGetProperty("updatedAt", out var uaProp))
                {
                    if (uaProp.TryGetProperty("integerValue", out var uaVal) && long.TryParse(uaVal.GetString(), out long u1))
                    {
                        updatedAt = u1;
                    }
                    else if (uaProp.TryGetProperty("timestampValue", out var tsVal) && DateTimeOffset.TryParse(tsVal.GetString(), out var u2))
                    {
                        updatedAt = u2.ToUnixTimeMilliseconds();
                    }
                }

                if (updatedAt <= 0 && fields.TryGetProperty("serverUpdatedAt", out var suaProp))
                {
                    if (suaProp.TryGetProperty("timestampValue", out var suaVal) && DateTimeOffset.TryParse(suaVal.GetString(), out var u3))
                    {
                        updatedAt = u3.ToUnixTimeMilliseconds();
                    }
                    else if (suaProp.TryGetProperty("integerValue", out var suaInt) && long.TryParse(suaInt.GetString(), out long u4))
                    {
                        updatedAt = u4;
                    }
                }

                if (updatedAt <= 0 && docElem.TryGetProperty("updateTime", out var utProp) && DateTimeOffset.TryParse(utProp.GetString(), out var u5))
                {
                    updatedAt = u5.ToUnixTimeMilliseconds();
                }

                if (updatedAt <= 0)
                {
                    updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                }

                // 5. Resolve Deletion Status
                bool isDeleted = fields.TryGetProperty("isDeleted", out var isDelProp) && isDelProp.TryGetProperty("booleanValue", out var isDelVal) && isDelVal.GetBoolean();

                long? deletedAt = null;
                if (fields.TryGetProperty("deletedAt", out var daProp))
                {
                    if (daProp.TryGetProperty("integerValue", out var daVal) && long.TryParse(daVal.GetString(), out long dVal))
                    {
                        deletedAt = dVal;
                    }
                    else if (daProp.TryGetProperty("timestampValue", out var daTs) && DateTimeOffset.TryParse(daTs.GetString(), out var dTsVal))
                    {
                        deletedAt = dTsVal.ToUnixTimeMilliseconds();
                    }
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

            Debug.WriteLine($"[Firestore] /users/{uid}/{collection}: Successfully downloaded {records.Count} records.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Firestore] EXCEPTION downloading /users/{uid}/{collection}: {ex.Message}");
        }

        return records;
    }

    public async Task RecordTombstoneAsync(string uid, string collection, string id, long deletedAt)
    {
        string url = $"{BaseFirestoreUrl}/users/{uid}/{collection}/{id}";
        Debug.WriteLine($"[Firestore] Recording tombstone for /users/{uid}/{collection}/{id} (deletedAt: {deletedAt})...");

        var fields = new Dictionary<string, object>
        {
            ["id"] = new { stringValue = id },
            ["collection"] = new { stringValue = collection },
            ["jsonPayload"] = new { stringValue = "" },
            ["updatedAt"] = new { integerValue = deletedAt.ToString() },
            ["serverUpdatedAt"] = new { timestampValue = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'") },
            ["isDeleted"] = new { booleanValue = true },
            ["deletedAt"] = new { integerValue = deletedAt.ToString() }
        };

        var body = new { fields };
        string jsonBody = JsonSerializer.Serialize(body);

        try
        {
            using var request = await CreateAuthorizedRequestAsync(HttpMethod.Patch, url);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                string err = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[Firestore] ERROR recording tombstone {id} in {collection}: HTTP {(int)response.StatusCode} {err}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Firestore] EXCEPTION recording tombstone {id} in {collection}: {ex.Message}");
        }
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
        Debug.WriteLine($"[Firestore] Clearing all user storage for UID {uid}...");
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
        Debug.WriteLine($"[Firestore] Cleared all user storage for UID {uid}.");
    }
}
