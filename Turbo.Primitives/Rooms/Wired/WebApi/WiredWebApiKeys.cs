using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Turbo.Primitives.Furniture.Enums;

namespace Turbo.Primitives.Rooms.Wired.WebApi;

/// <summary>
/// The keys of a "Variable Global Add-on: Web API" (<c>wf_xtra_var_web_api</c>), as its editor
/// saves them: the string param <c>readKey + "\t" + writeKey</c> (Flash
/// <c>VariablesWebApiAddon.readStringParamFromForm</c>). Shared by the box, the endpoints that
/// check a key before asking the room, and anything that copies furni between rooms.
/// </summary>
public static class WiredWebApiKeys
{
    public const string ADDON_LOGIC = "wf_xtra_var_web_api";

    public const char SEPARATOR = '\t';

    public static string ReadKeyOf(string? stringParam) => KeyAt(stringParam, 0);

    public static string WriteKeyOf(string? stringParam) => KeyAt(stringParam, 1);

    /// <summary>Whether <paramref name="sent"/> matches <paramref name="key"/> (a set key, compared in constant time).</summary>
    public static bool Matches(string? sent, string key) =>
        key.Length > 0
        && sent is not null
        && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(sent),
            Encoding.UTF8.GetBytes(key)
        );

    /// <summary>Which keys a call carries: reads need the read key, writes the write key, a batch both.</summary>
    public static (bool Read, bool Write) KeysNeeded(WiredWebApiOperation operation) =>
        operation switch
        {
            WiredWebApiOperation.Batch => (true, true),
            WiredWebApiOperation.PutValue
            or WiredWebApiOperation.PatchValue
            or WiredWebApiOperation.DeleteValue
            or WiredWebApiOperation.BulkDelete
            or WiredWebApiOperation.PatchGlobal
            or WiredWebApiOperation.PatchUserProfile
            or WiredWebApiOperation.DeleteUserProfile
            or WiredWebApiOperation.PatchFurniProfile
            or WiredWebApiOperation.PatchGlobalProfile => (false, true),
            _ => (true, false),
        };

    /// <summary>The string param an add-on's stored extra data holds (its wired section), empty when none.</summary>
    public static string StoredStringParam(string? extraData)
    {
        if (string.IsNullOrWhiteSpace(extraData))
            return string.Empty;

        try
        {
            using var document = JsonDocument.Parse(extraData);

            if (
                document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(ExtraDataSectionType.WIRED, out var wired)
                || wired.ValueKind != JsonValueKind.Object
            )
                return string.Empty;

            foreach (var property in wired.EnumerateObject())
            {
                if (
                    string.Equals(property.Name, "stringParam", StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.String
                )
                    return property.Value.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            // Not JSON: no keys.
        }

        return string.Empty;
    }

    private static string KeyAt(string? stringParam, int index)
    {
        var parts = (stringParam ?? string.Empty).Split(SEPARATOR);

        return index < parts.Length ? parts[index].Trim() : string.Empty;
    }
}
