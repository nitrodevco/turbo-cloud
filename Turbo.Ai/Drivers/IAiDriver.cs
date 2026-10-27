using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
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

    /// <summary>
    /// Asks the provider for its answer as it writes it: <paramref name="text"/> is given each
    /// piece of text as it comes, and the whole answer is returned as <see cref="SendAsync"/>
    /// returns it. A driver that can't stream answers whole, its text as one piece.
    /// </summary>
    async Task<AiDriverAnswer> StreamAsync(
        AiEndpoint endpoint,
        AiProviderRequest request,
        Action<string> text,
        CancellationToken ct
    )
    {
        var answer = await SendAsync(endpoint, request, ct).ConfigureAwait(false);

        if (answer.Message.Text is { Length: > 0 } said)
            text(said);

        return answer;
    }
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

/// <summary>One event of a streamed answer: its name (null when it has none) and its data.</summary>
public sealed record ServerSentEvent(string? Name, string Data);

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
        using var response = await OpenAsync(
                http,
                request,
                endpoint,
                readError,
                HttpCompletionOption.ResponseContentRead,
                ct
            )
            .ConfigureAwait(false);

        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a request whose answer is streamed (<c>text/event-stream</c>) and reads its events
    /// as they come, the answer's status checked first.
    /// </summary>
    public static async IAsyncEnumerable<ServerSentEvent> StreamAsync(
        HttpClient http,
        HttpRequestMessage request,
        AiEndpoint endpoint,
        Func<string, string?> readError,
        [EnumeratorCancellation] CancellationToken ct
    )
    {
        using var response = await OpenAsync(
                http,
                request,
                endpoint,
                readError,
                HttpCompletionOption.ResponseHeadersRead,
                ct
            )
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

        await using (body.ConfigureAwait(false))
        {
            await foreach (var e in ReadEventsAsync(body, endpoint, ct).ConfigureAwait(false))
                yield return e;
        }
    }

    /// <summary>
    /// The events of a <c>text/event-stream</c> body: each one's <c>event:</c> name (null when it
    /// has none) and its <c>data:</c> lines joined.
    /// </summary>
    public static async IAsyncEnumerable<ServerSentEvent> ReadEventsAsync(
        Stream body,
        AiEndpoint? endpoint,
        [EnumeratorCancellation] CancellationToken ct
    )
    {
        using var reader = new StreamReader(body, Encoding.UTF8);
        string? name = null;
        var data = new StringBuilder();
        var any = false;

        while (true)
        {
            string? line;

            try
            {
                line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            }
            catch (IOException ex)
            {
                throw new AiProviderException(
                    "network",
                    Redact("stopped answering part way", endpoint?.Key),
                    ex
                );
            }

            if (line is null || line.Length == 0)
            {
                if (any)
                    yield return new ServerSentEvent(name, data.ToString());

                if (line is null)
                    yield break;

                name = null;
                data.Clear();
                any = false;

                continue;
            }

            if (line[0] == ':')
                continue;

            var colon = line.IndexOf(':');
            var field = colon < 0 ? line : line[..colon];
            var value = colon < 0 ? string.Empty : line[(colon + 1)..];

            if (value.StartsWith(' '))
                value = value[1..];

            switch (field)
            {
                case "event":
                    name = value;
                    any = true;

                    break;
                case "data":
                    if (data.Length > 0)
                        data.Append('\n');

                    data.Append(value);
                    any = true;

                    break;
            }
        }
    }

    private static async Task<HttpResponseMessage> OpenAsync(
        HttpClient http,
        HttpRequestMessage request,
        AiEndpoint endpoint,
        Func<string, string?> readError,
        HttpCompletionOption completion,
        CancellationToken ct
    )
    {
        HttpResponseMessage response;

        try
        {
            response = await http.SendAsync(request, completion, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new AiProviderException(
                "network",
                Redact($"could not reach it ({ex.HttpRequestError})", endpoint.Key),
                ex
            );
        }

        if (response.IsSuccessStatusCode)
            return response;

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var status = (int)response.StatusCode;
            var why = readError(body) ?? response.ReasonPhrase ?? "no reason given";

            throw new AiProviderException(
                $"http_{status}",
                Redact($"answered {status}: {Shorten(why)}", endpoint.Key)
            );
        }
    }

    /// <summary>A provider's error sent inside a streamed answer, as an <see cref="AiProviderException"/>.</summary>
    public static AiProviderException StreamError(string kind, string? why, AiEndpoint? endpoint) =>
        new(kind, Redact($"failed part way: {Shorten(why ?? "no reason given")}", endpoint?.Key));

    /// <summary>A model's tool arguments, written a piece at a time, read when the call is whole.</summary>
    public static JsonElement ParsePieces(StringBuilder pieces) =>
        pieces.Length == 0
            ? JsonSerializer.SerializeToElement(new JsonObject())
            : ParseArguments(pieces.ToString());

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
