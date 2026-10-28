using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Ai;

namespace Turbo.Ai.Drivers;

/// <summary>
/// OpenAI's chat completions (<c>POST {Url}/chat/completions</c>), which OpenAI and most other
/// servers speak, local ones included: the instructions as a <c>system</c> message, tools as
/// <c>function</c>s, <c>tool_calls</c> answered by <c>tool</c> messages.
/// </summary>
public sealed class OpenAiCompatibleDriver(IHttpClientFactory http) : IAiDriver
{
    public const string NAME = "openai-compatible";

    public const string DEFAULT_URL = "https://api.openai.com/v1";

    public string Name => NAME;

    public async Task<AiDriverAnswer> SendAsync(
        AiEndpoint endpoint,
        AiProviderRequest request,
        CancellationToken ct
    )
    {
        using var message = Build(endpoint, request);
        var body = await DriverHttp
            .SendAsync(http.CreateClient(AiModule.HTTP_CLIENT), message, endpoint, ReadError, ct)
            .ConfigureAwait(false);

        return Read(body);
    }

    public async Task<AiDriverAnswer> StreamAsync(
        AiEndpoint endpoint,
        AiProviderRequest request,
        Action<string> text,
        CancellationToken ct
    )
    {
        using var message = Build(endpoint, request, stream: true);

        return await ReadStreamAsync(
                DriverHttp.StreamAsync(
                    http.CreateClient(AiModule.HTTP_CLIENT),
                    message,
                    endpoint,
                    ReadError,
                    ct
                ),
                text,
                endpoint
            )
            .ConfigureAwait(false);
    }

    public static HttpRequestMessage Build(
        AiEndpoint endpoint,
        AiProviderRequest request,
        bool stream = false
    )
    {
        var url = string.IsNullOrWhiteSpace(endpoint.Url) ? DEFAULT_URL : endpoint.Url;
        var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"{url.TrimEnd('/')}/chat/completions"
        )
        {
            Content = new StringContent(
                Body(request, endpoint, stream).ToJsonString(),
                Encoding.UTF8,
                "application/json"
            ),
        };

        if (endpoint.Key is { } key)
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

        foreach (var (name, value) in endpoint.Headers)
            message.Headers.TryAddWithoutValidation(name, value);

        return message;
    }

    public static JsonObject Body(
        AiProviderRequest request,
        AiEndpoint? endpoint = null,
        bool stream = false
    )
    {
        var messages = new JsonArray();

        if (!string.IsNullOrWhiteSpace(request.Instructions))
            messages.Add(
                new JsonObject { ["role"] = "system", ["content"] = request.Instructions }
            );

        foreach (var message in request.Messages)
        foreach (var written in Write(message))
            messages.Add(written);

        var body = new JsonObject { ["model"] = request.Model, ["messages"] = messages };

        // OpenAI's own API takes max_completion_tokens (its newer models refuse max_tokens); the
        // servers that copy its API take max_tokens.
        body[IsOpenAi(endpoint) ? "max_completion_tokens" : "max_tokens"] = request.MaxTokens;

        if (request.Temperature is { } temperature)
            body["temperature"] = temperature;

        if (request.Tools.Length > 0)
        {
            body["tools"] = new JsonArray([
                .. request.Tools.Select(t => new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = t.Name,
                        ["description"] = t.Description,
                        ["parameters"] = JsonNode.Parse(t.InputSchema.GetRawText()),
                    },
                }),
            ]);

            if (request.RequireTool)
                body["tool_choice"] = "required";
        }

        if (stream)
        {
            body["stream"] = true;
            // Without it a streamed answer gives no token counts, and budgets could not count it.
            body["stream_options"] = new JsonObject { ["include_usage"] = true };
        }

        if (endpoint is not null)
            DriverHttp.AddOptions(body, endpoint.Options);

        return body;
    }

    private static bool IsOpenAi(AiEndpoint? endpoint) =>
        endpoint is null
        || string.IsNullOrWhiteSpace(endpoint.Url)
        || (
            Uri.TryCreate(endpoint.Url, UriKind.Absolute, out var uri)
            && uri.Host.Equals("api.openai.com", StringComparison.OrdinalIgnoreCase)
        );

    /// <summary>One SDK message as chat messages: a tool's results each as a <c>tool</c> message, before any text.</summary>
    private static IEnumerable<JsonObject> Write(AiMessage message)
    {
        if (message.Role == AiRole.User)
        {
            foreach (var result in message.Parts.OfType<AiToolResultPart>())
                yield return new JsonObject
                {
                    ["role"] = "tool",
                    ["tool_call_id"] = result.CallId,
                    ["content"] = result.IsError ? $"Error: {result.Text}" : result.Text,
                };

            if (message.Text.Length > 0)
                yield return new JsonObject { ["role"] = "user", ["content"] = message.Text };

            yield break;
        }

        var assistant = new JsonObject
        {
            ["role"] = "assistant",
            ["content"] = message.Text.Length > 0 ? message.Text : null,
        };
        var calls = message.Parts.OfType<AiToolCallPart>().ToList();

        if (calls.Count > 0)
            assistant["tool_calls"] = new JsonArray([
                .. calls.Select(call => new JsonObject
                {
                    ["id"] = call.Id,
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = call.Name,
                        ["arguments"] = call.Arguments.GetRawText(),
                    },
                }),
            ]);

        yield return assistant;
    }

    public static AiDriverAnswer Read(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var choice = root.GetProperty("choices")[0];
            var message = choice.GetProperty("message");
            var parts = ImmutableArray.CreateBuilder<AiPart>();

            if (
                message.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.String
                && content.GetString() is { Length: > 0 } text
            )
                parts.Add(new AiTextPart(text));

            if (
                message.TryGetProperty("tool_calls", out var calls)
                && calls.ValueKind == JsonValueKind.Array
            )
                foreach (var call in calls.EnumerateArray())
                {
                    var function = call.GetProperty("function");

                    parts.Add(
                        new AiToolCallPart(
                            call.TryGetProperty("id", out var id)
                                ? id.GetString() ?? ""
                                : $"call_{parts.Count}",
                            function.GetProperty("name").GetString() ?? "",
                            DriverHttp.ParseArguments(
                                function.TryGetProperty("arguments", out var args)
                                    ? args.ValueKind == JsonValueKind.String
                                        ? args.GetString()
                                        : args.GetRawText()
                                    : null
                            )
                        )
                    );
                }

            var finish = Finish(
                parts,
                choice.TryGetProperty("finish_reason", out var reason) ? reason.GetString() : null
            );
            var input = 0;
            var output = 0;

            if (
                root.TryGetProperty("usage", out var usage)
                && usage.ValueKind == JsonValueKind.Object
            )
            {
                input = Int(usage, "prompt_tokens");
                output = Int(usage, "completion_tokens");
            }

            return new AiDriverAnswer(
                new AiMessage(AiRole.Assistant, parts.ToImmutable()),
                finish,
                input,
                output,
                root.TryGetProperty("model", out var model) ? model.GetString() ?? "" : ""
            );
        }
        catch (Exception ex)
            when (ex
                    is JsonException
                        or KeyNotFoundException
                        or InvalidOperationException
                        or IndexOutOfRangeException
            )
        {
            throw new AiProviderException(
                "unreadable",
                "answered something that is not a chat completion",
                ex
            );
        }
    }

    /// <summary>
    /// A streamed answer (<c>"stream": true</c>): chunks whose <c>choices[0].delta</c> holds a
    /// piece of <c>content</c> or of <c>tool_calls</c> (by <c>index</c>: the id and name first,
    /// the arguments in pieces), a <c>finish_reason</c>, a last chunk with the <c>usage</c>
    /// (<c>stream_options.include_usage</c>), then <c>[DONE]</c>; a chunk with an <c>error</c>
    /// fails it. Each piece of text goes to <paramref name="text"/> as it comes.
    /// </summary>
    public static async Task<AiDriverAnswer> ReadStreamAsync(
        IAsyncEnumerable<ServerSentEvent> events,
        Action<string> text,
        AiEndpoint? endpoint = null
    )
    {
        var content = new StringBuilder();
        var calls = new SortedDictionary<int, (string Id, string Name, StringBuilder Arguments)>();
        string? reason = null;
        var input = 0;
        var output = 0;
        var model = string.Empty;
        var done = false;

        try
        {
            await foreach (var e in events.ConfigureAwait(false))
            {
                if (e.Data == "[DONE]")
                {
                    done = true;

                    break;
                }

                if (e.Data.Length == 0)
                    continue;

                using var doc = JsonDocument.Parse(e.Data);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var error))
                    throw DriverHttp.StreamError(
                        "stream_error",
                        error.ValueKind == JsonValueKind.Object
                            ? error.TryGetProperty("message", out var m)
                                ? m.GetString()
                                : null
                            : error.GetString(),
                        endpoint
                    );

                if (root.TryGetProperty("model", out var named) && named.GetString() is { } name)
                    model = name;

                if (
                    root.TryGetProperty("usage", out var usage)
                    && usage.ValueKind == JsonValueKind.Object
                )
                {
                    input = Int(usage, "prompt_tokens");
                    output = Int(usage, "completion_tokens");
                }

                if (
                    !root.TryGetProperty("choices", out var choices)
                    || choices.ValueKind != JsonValueKind.Array
                    || choices.GetArrayLength() == 0
                )
                    continue;

                var choice = choices[0];

                if (choice.TryGetProperty("delta", out var delta))
                {
                    if (
                        delta.TryGetProperty("content", out var piece)
                        && piece.ValueKind == JsonValueKind.String
                        && piece.GetString() is { Length: > 0 } said
                    )
                    {
                        content.Append(said);
                        text(said);
                    }

                    if (
                        delta.TryGetProperty("tool_calls", out var deltas)
                        && deltas.ValueKind == JsonValueKind.Array
                    )
                        foreach (var call in deltas.EnumerateArray())
                        {
                            var index = call.TryGetProperty("index", out var i)
                                ? i.GetInt32()
                                : calls.Count;

                            if (!calls.TryGetValue(index, out var into))
                                calls[index] = into = (
                                    $"call_{index}",
                                    string.Empty,
                                    new StringBuilder()
                                );

                            if (call.TryGetProperty("id", out var id) && id.GetString() is { } x)
                                into.Id = x;

                            if (call.TryGetProperty("function", out var function))
                            {
                                if (
                                    function.TryGetProperty("name", out var n)
                                    && n.GetString() is { Length: > 0 } called
                                )
                                    into.Name = called;

                                if (
                                    function.TryGetProperty("arguments", out var args)
                                    && args.ValueKind == JsonValueKind.String
                                )
                                    into.Arguments.Append(args.GetString());
                            }

                            calls[index] = into;
                        }
                }

                if (
                    choice.TryGetProperty("finish_reason", out var finished)
                    && finished.ValueKind == JsonValueKind.String
                )
                    reason = finished.GetString();
            }
        }
        catch (Exception ex)
            when (ex
                    is JsonException
                        or KeyNotFoundException
                        or InvalidOperationException
                        or IndexOutOfRangeException
                        or FormatException
            )
        {
            throw new AiProviderException(
                "unreadable",
                "streamed something that is not a chat completion",
                ex
            );
        }

        // Some servers end without [DONE]; an answer that never said why it ended was cut off.
        if (!done && reason is null)
            throw new AiProviderException("unreadable", "stopped answering part way");

        var parts = ImmutableArray.CreateBuilder<AiPart>();

        if (content.Length > 0)
            parts.Add(new AiTextPart(content.ToString()));

        foreach (var (id, name, arguments) in calls.Values)
            parts.Add(new AiToolCallPart(id, name, DriverHttp.ParsePieces(arguments)));

        return new AiDriverAnswer(
            new AiMessage(AiRole.Assistant, parts.ToImmutable()),
            Finish(parts, reason),
            input,
            output,
            model
        );
    }

    // Some servers say "stop" with tool calls: the calls decide.
    private static AiFinish Finish(ImmutableArray<AiPart>.Builder parts, string? reason) =>
        parts.OfType<AiToolCallPart>().Any()
            ? AiFinish.ToolUse
            : reason switch
            {
                "length" => AiFinish.MaxTokens,
                "content_filter" => AiFinish.Refused,
                _ => AiFinish.EndTurn,
            };

    private static string? ReadError(string body) =>
        DriverHttp.ErrorMessage(
            body,
            root =>
                root.TryGetProperty("error", out var error)
                    ? error.ValueKind == JsonValueKind.Object
                        ? error.TryGetProperty("message", out var m)
                            ? m.GetString()
                            : null
                        : error.GetString()
                    : null
        );

    private static int Int(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;
}
