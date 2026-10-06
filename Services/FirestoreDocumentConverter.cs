using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace FinPulse.Windows.Services;

/// <summary>
/// Bidirectional converter between Firestore REST document format and standard JSON / domain models.
/// Guarantees 100% schema parity with FinPulse Android, supporting both native Firestore fields
/// and jsonPayload envelopes.
/// </summary>
public static class FirestoreDocumentConverter
{
    public static string ConvertFieldsToJson(JsonElement fieldsElement, string? docId = null)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();

            bool hasId = false;
            if (fieldsElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in fieldsElement.EnumerateObject())
                {
                    if (prop.NameEquals("id")) hasId = true;
                    writer.WritePropertyName(prop.Name);
                    WriteFirestoreValue(writer, prop.Value);
                }
            }

            if (!hasId && !string.IsNullOrWhiteSpace(docId))
            {
                writer.WriteString("id", docId);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteFirestoreValue(Utf8JsonWriter writer, JsonElement valElem)
    {
        if (valElem.ValueKind != JsonValueKind.Object)
        {
            valElem.WriteTo(writer);
            return;
        }

        if (valElem.TryGetProperty("stringValue", out var sProp))
        {
            writer.WriteStringValue(sProp.GetString());
        }
        else if (valElem.TryGetProperty("integerValue", out var iProp))
        {
            if (long.TryParse(iProp.GetString(), out long lVal))
                writer.WriteNumberValue(lVal);
            else
                writer.WriteStringValue(iProp.GetString());
        }
        else if (valElem.TryGetProperty("doubleValue", out var dProp))
        {
            if (dProp.ValueKind == JsonValueKind.Number)
                writer.WriteNumberValue(dProp.GetDouble());
            else if (double.TryParse(dProp.GetString(), out double dVal))
                writer.WriteNumberValue(dVal);
            else
                writer.WriteNumberValue(0.0);
        }
        else if (valElem.TryGetProperty("booleanValue", out var bProp))
        {
            writer.WriteBooleanValue(bProp.GetBoolean());
        }
        else if (valElem.TryGetProperty("timestampValue", out var tsProp))
        {
            string tsStr = tsProp.GetString() ?? "";
            if (DateTimeOffset.TryParse(tsStr, out var dto))
            {
                writer.WriteNumberValue(dto.ToUnixTimeMilliseconds());
            }
            else
            {
                writer.WriteStringValue(tsStr);
            }
        }
        else if (valElem.TryGetProperty("nullValue", out _))
        {
            writer.WriteNullValue();
        }
        else if (valElem.TryGetProperty("mapValue", out var mapProp))
        {
            writer.WriteStartObject();
            if (mapProp.TryGetProperty("fields", out var subFields) && subFields.ValueKind == JsonValueKind.Object)
            {
                foreach (var subProp in subFields.EnumerateObject())
                {
                    writer.WritePropertyName(subProp.Name);
                    WriteFirestoreValue(writer, subProp.Value);
                }
            }
            writer.WriteEndObject();
        }
        else if (valElem.TryGetProperty("arrayValue", out var arrProp))
        {
            writer.WriteStartArray();
            if (arrProp.TryGetProperty("values", out var vals) && vals.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in vals.EnumerateArray())
                {
                    WriteFirestoreValue(writer, item);
                }
            }
            writer.WriteEndArray();
        }
        else
        {
            writer.WriteNullValue();
        }
    }

    public static Dictionary<string, object> ConvertJsonToFirestoreFields(
        string jsonPayload,
        string docId,
        string collection,
        long updatedAt,
        bool isDeleted,
        long? deletedAt)
    {
        var fields = new Dictionary<string, object>
        {
            ["id"] = new { stringValue = docId },
            ["collection"] = new { stringValue = collection },
            ["jsonPayload"] = new { stringValue = jsonPayload },
            ["updatedAt"] = new { integerValue = updatedAt.ToString() },
            ["serverUpdatedAt"] = new { timestampValue = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'") },
            ["isDeleted"] = new { booleanValue = isDeleted }
        };

        if (deletedAt.HasValue)
        {
            fields["deletedAt"] = new { integerValue = deletedAt.Value.ToString() };
        }

        // Parse jsonPayload to write native fields into Firestore document for Android & Web parity
        if (!string.IsNullOrWhiteSpace(jsonPayload))
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonPayload);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        // Don't overwrite envelope fields
                        if (prop.NameEquals("id") || prop.NameEquals("collection") || prop.NameEquals("jsonPayload") ||
                            prop.NameEquals("updatedAt") || prop.NameEquals("isDeleted") || prop.NameEquals("deletedAt"))
                        {
                            continue;
                        }

                        var fv = ConvertJsonElementToFirestoreValue(prop.Value);
                        if (fv != null)
                        {
                            fields[prop.Name] = fv;
                        }
                    }
                }
            }
            catch { }
        }

        return fields;
    }

    private static object? ConvertJsonElementToFirestoreValue(JsonElement elem)
    {
        return elem.ValueKind switch
        {
            JsonValueKind.String => new { stringValue = elem.GetString() },
            JsonValueKind.Number => elem.TryGetInt64(out long l)
                ? new { integerValue = l.ToString() }
                : new { doubleValue = elem.GetDouble() },
            JsonValueKind.True => new { booleanValue = true },
            JsonValueKind.False => new { booleanValue = false },
            JsonValueKind.Null => new { nullValue = (object?)null },
            JsonValueKind.Array => new
            {
                arrayValue = new
                {
                    values = elem.EnumerateArray()
                        .Select(ConvertJsonElementToFirestoreValue)
                        .Where(v => v != null)
                        .ToList()
                }
            },
            JsonValueKind.Object => new
            {
                mapValue = new
                {
                    fields = elem.EnumerateObject()
                        .ToDictionary(p => p.Name, p => ConvertJsonElementToFirestoreValue(p.Value)!)
                }
            },
            _ => null
        };
    }
}
