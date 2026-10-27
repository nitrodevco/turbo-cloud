using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Ai;

namespace Turbo.Ai.Drivers;

/// <summary>
/// Speaks one kind of provider's API: writes a step as its request and reads its answer back as
/// the SDK's message. A provider in <c>Turbo:Ai:Providers</c> names its driver.
/// </summary>
public interface IAiDriver
{
    /// <summary>What a provider's <c>Driver</c> names it by.</summary>
    string Name { get; }

    /// <summary>Asks the provider. Throws <see cref="AiProviderException"/> when it can't answer.</summary>
    Task<AiDriverAnswer> SendAsync(
        AiEndpoint endpoint,
        AiProviderRequest request,
        CancellationToken ct
    );
}

/// <summary>Where and how to reach one provider: its name, address, key (null for none), headers and extra body fields.</summary>
public sealed record AiEndpoint(
    string Name,
    string Url,
    string? Key,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyDictionary<string, string> Options
);

/// <summary>One step as a provider is asked it.</summary>
public sealed record AiProviderRequest(
    string Model,
    string Instructions,
    ImmutableArray<AiMessage> Messages,
    ImmutableArray<AiToolSpec> Tools,
    bool RequireTool,
    int MaxTokens,
    double? Temperature
);

/// <summary>A provider's answer: the message, why it ended, its tokens and the model that answered.</summary>
public sealed record AiDriverAnswer(
    AiMessage Message,
    AiFinish Finish,
    int InputTokens,
    int OutputTokens,
    string Model
);

/// <summary>
/// A provider could not answer: <see cref="Kind"/> is what the usage log keeps (<c>timeout</c>,
/// <c>http_429</c>, <c>network</c>, <c>unreadable</c>); the message never holds a key.
/// </summary>
public sealed class AiProviderException(string kind, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public string Kind { get; } = kind;
}

/// <summary>What the HTTP drivers share: sending, errors, extra fields and keeping keys out of messages.</summary>
internal static partial class DriverHttp
{
    public static async Task<string> SendAsync(
        HttpClient http,
        HttpRequestMessage request,
        AiEndpoint endpoint,
        Func<string, string?> readError,
        CancellationToken ct
    )
    {
        HttpResponseMessage response;

        try
        {
            response = await http.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new AiProviderException(
                "network",
                Redact($"could not reach it ({ex.HttpRequestError})", endpoint.Key),
                ex
            );
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
                return body;

            var status = (int)response.StatusCode;
            var why = readError(body) ?? response.ReasonPhrase ?? "no reason given";

            throw new AiProviderException(
                $"http_{status}",
                Redact($"answered {status}: {Shorten(why)}", endpoint.Key)
            );
        }
    }

    /// <summary>Adds the provider's extra body fields where the driver wrote none of the same name.</summary>
    public static void AddOptions(JsonObject body, IReadOnlyDictionary<string, string> options)
    {
        foreach (var (name, raw) in options)
        {
            if (body.ContainsKey(name))
                continue;

            JsonNode? value;

            try
            {
                value = JsonNode.Parse(raw);
            }
            catch (JsonException)
            {
                value = JsonValue.Create(raw);
            }

            body[name] = value;
        }
    }

    public static string? ErrorMessage(string body, Func<JsonElement, string?> read)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);

            return read(doc.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The text with the key, and anything shaped like a key, taken out.</summary>
    public static string Redact(string text, string? key)
    {
        if (!string.IsNullOrEmpty(key))
            text = text.Replace(key, "[key]", StringComparison.Ordinal);

        return KeyLike().Replace(text, "[key]");
    }

    public static JsonElement ParseArguments(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return JsonSerializer.SerializeToElement(new JsonObject());

        try
        {
            using var doc = JsonDocument.Parse(raw);

            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            // The model wrote arguments that are not JSON: kept as text, which the tool's schema
            // check answers with a sentence the model can correct them by.
            return JsonSerializer.SerializeToElement(raw);
        }
    }

    private static string Shorten(string text) => text.Length <= 300 ? text : text[..297] + "...";

    [GeneratedRegex(@"\b(sk|key|pk)-[A-Za-z0-9_\-\*]{6,}")]
    private static partial Regex KeyLike();
}
