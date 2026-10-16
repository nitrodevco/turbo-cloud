using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Primitives.Rooms.Wired.WebApi;

namespace Turbo.Rooms.Wired.WebApi;

/// <summary>
/// The Web API's JSON (Habbo's OpenAPI document): a value is a signed 64-bit integer written as a
/// decimal string ("to preserve precision"), times are UTC ISO 8601 to the second, and an error
/// is <c>{ "error": code }</c>.
/// </summary>
public static partial class WiredWebApiJson
{
    public static readonly WiredWebApiResponse NoContent = new(204, null);

    public static WiredWebApiResponse Ok(JsonNode body) => new(200, body.ToJsonString());

    public static WiredWebApiResponse Error(int status, string code) =>
        new(status, new JsonObject { ["error"] = code }.ToJsonString());

    /// <summary><c>WiredVariable</c>: value, creation_time, update_time (a time not known is left out).</summary>
    public static JsonObject Variable(WiredVariableValue value, long createdAtMs, long updatedAtMs)
    {
        var json = new JsonObject
        {
            ["value"] = value.Value.ToString(CultureInfo.InvariantCulture),
        };

        if (createdAtMs > 0)
            json["creation_time"] = Time(createdAtMs);

        if (updatedAtMs > 0)
            json["update_time"] = Time(updatedAtMs);

        return json;
    }

    /// <summary>
    /// A value to write: the document's decimal string (<c>^-?\d+$</c>), or a JSON integer, which
    /// a client library may send for a small one.
    /// </summary>
    public static bool TryReadValue(JsonNode? node, out WiredVariableValue value)
    {
        value = default;

        if (node is not JsonValue json)
            return false;

        if (json.TryGetValue<string>(out var text))
        {
            if (
                !ValuePattern().IsMatch(text)
                || !long.TryParse(
                    text,
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out var parsed
                )
            )
                return false;

            value = new WiredVariableValue(parsed);

            return true;
        }

        if (json.GetValueKind() == JsonValueKind.Number && json.TryGetValue<long>(out var number))
        {
            value = new WiredVariableValue(number);

            return true;
        }

        return false;
    }

    public static string? StringOf(JsonNode? node) =>
        node is JsonValue json && json.TryGetValue<string>(out var text) ? text : null;

    /// <summary>
    /// One batch result: <c>op_id</c> (null when none was sent), the status, and the body for a
    /// 200 or <c>{ code, message }</c> for an error, whose message "mirrors the error code".
    /// </summary>
    public static JsonObject BatchResult(string? opId, WiredWebApiResponse response)
    {
        var result = new JsonObject { ["op_id"] = opId, ["status"] = response.Status };

        if (response.Json is null)
            return result;

        var body = JsonNode.Parse(response.Json);

        if (response.Status == 200)
        {
            result["body"] = body;

            return result;
        }

        var code = StringOf(body?["error"]) ?? string.Empty;

        result["error"] = new JsonObject { ["code"] = code, ["message"] = code };

        return result;
    }

    private static string Time(long ms) =>
        DateTimeOffset
            .FromUnixTimeMilliseconds(ms)
            .UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^-?\d+$")]
    private static partial Regex ValuePattern();
}
