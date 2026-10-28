using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Ai;

namespace Turbo.Ai.Drivers;

/// <summary>
/// Anthropic's Messages API (<c>POST /v1/messages</c>): the instructions as <c>system</c>, tools
/// with <c>input_schema</c>, <c>tool_use</c> blocks answered by <c>tool_result</c> blocks in the
/// next user turn.
/// </summary>
public sealed class AnthropicDriver(IHttpClientFactory http) : IAiDriver
{
    public const string NAME = "anthropic";

    public const string DEFAULT_URL = "https://api.anthropic.com";

    public const string VERSION = "2023-06-01";

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
        var message = new HttpRequestMessage(HttpMethod.Post, $"{url.TrimEnd('/')}/v1/messages")
        {
            Content = new StringContent(
                Body(request, endpoint, stream).ToJsonString(),
                Encoding.UTF8,
                "application/json"
            ),
        };

        message.Headers.Add("anthropic-version", VERSION);

        if (endpoint.Key is { } key)
            message.Headers.Add("x-api-key", key);

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
        var body = new JsonObject { ["model"] = request.Model, ["max_tokens"] = request.MaxTokens };

        if (!string.IsNullOrWhiteSpace(request.Instructions))
            body["system"] = request.Instructions;

        body["messages"] = Messages(request.Messages);

        if (request.Temperature is { } temperature)
            body["temperature"] = temperature;

        if (request.Tools.Length > 0)
        {
            body["tools"] = new JsonArray([
                .. request.Tools.Select(t => new JsonObject
                {
                    ["name"] = t.Name,
                    ["description"] = t.Description,
                    ["input_schema"] = JsonNode.Parse(t.InputSchema.GetRawText()),
                }),
            ]);

            if (request.RequireTool)
                body["tool_choice"] = new JsonObject { ["type"] = "any" };
        }

        if (stream)
            body["stream"] = true;

        if (endpoint is not null)
            DriverHttp.AddOptions(body, endpoint.Options);

        return body;
    }

    /// <summary>The conversation as Anthropic's turns; two in a row of one role become one turn.</summary>
    private static JsonArray Messages(ImmutableArray<AiMessage> messages)
    {
        var turns = new JsonArray();
        JsonObject? last = null;

        foreach (var message in messages)
        {
            var role = message.Role == AiRole.User ? "user" : "assistant";
            var blocks = new JsonArray();

            foreach (var part in message.Parts)
            {
                switch (part)
                {
                    case AiTextPart { Text.Length: > 0 } text:
                        blocks.Add(new JsonObject { ["type"] = "text", ["text"] = text.Text });

                        break;
                    case AiToolCallPart call:
                        blocks.Add(
                            new JsonObject
                            {
                                ["type"] = "tool_use",
                                ["id"] = call.Id,
                                ["name"] = call.Name,
                                ["input"] =
                                    call.Arguments.ValueKind == JsonValueKind.Object
                                        ? JsonNode.Parse(call.Arguments.GetRawText())
                                        : new JsonObject(),
                            }
                        );

                        break;
                    case AiToolResultPart result:
                        var block = new JsonObject
                        {
                            ["type"] = "tool_result",
                            ["tool_use_id"] = result.CallId,
                            ["content"] = result.Text,
                        };

                        if (result.IsError)
                            block["is_error"] = true;

                        blocks.Add(block);

                        break;
                }
            }

            if (blocks.Count == 0)
                continue;

            if (last is not null && (string?)last["role"] == role)
            {
                var into = (JsonArray)last["content"]!;

                foreach (var block in blocks.ToList())
                {
                    blocks.Remove(block);
                    into.Add(block);
                }

                continue;
            }

            last = new JsonObject { ["role"] = role, ["content"] = blocks };
            turns.Add(last);
        }

        return turns;
    }

    public static AiDriverAnswer Read(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var parts = ImmutableArray.CreateBuilder<AiPart>();

            foreach (var block in root.GetProperty("content").EnumerateArray())
            {
                switch (block.GetProperty("type").GetString())
                {
                    case "text":
                        parts.Add(new AiTextPart(block.GetProperty("text").GetString() ?? ""));

                        break;
                    case "tool_use":
                        parts.Add(
                            new AiToolCallPart(
                                block.GetProperty("id").GetString() ?? "",
                                block.GetProperty("name").GetString() ?? "",
                                block.GetProperty("input").Clone()
                            )
                        );

                        break;
                }
            }

            var finish = Finish(
                root.TryGetProperty("stop_reason", out var stop) ? stop.GetString() : null
            );
            var input = 0;
            var output = 0;

            if (root.TryGetProperty("usage", out var usage))
            {
                input = InputTokens(usage);
                output = Int(usage, "output_tokens");
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
            when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new AiProviderException(
                "unreadable",
                "answered something that is not a message",
                ex
            );
        }
    }

    /// <summary>
    /// A streamed answer (<c>"stream": true</c>): <c>message_start</c> with the input tokens,
    /// <c>content_block_start</c>/<c>_delta</c>/<c>_stop</c> per block (<c>text_delta</c> text,
    /// <c>input_json_delta</c> a tool call's arguments), <c>message_delta</c> with the stop reason
    /// and output tokens, <c>message_stop</c>; an <c>error</c> event fails it. Each piece of text
    /// goes to <paramref name="text"/> as it comes.
    /// </summary>
    public static async Task<AiDriverAnswer> ReadStreamAsync(
        IAsyncEnumerable<ServerSentEvent> events,
        Action<string> text,
        AiEndpoint? endpoint = null
    )
    {
        var blocks = new SortedDictionary<int, StreamedBlock?>();
        var input = 0;
        var output = 0;
        var model = string.Empty;
        string? stop = null;
        var ended = false;

        try
        {
            await foreach (var e in events.ConfigureAwait(false))
            {
                if (e.Data.Length == 0)
                    continue;

                using var doc = JsonDocument.Parse(e.Data);
                var root = doc.RootElement;

                switch (Str(root, "type") ?? e.Name)
                {
                    case "message_start":
                        var started = root.GetProperty("message");

                        model = Str(started, "model") ?? string.Empty;

                        if (started.TryGetProperty("usage", out var first))
                        {
                            input = InputTokens(first);
                            output = Int(first, "output_tokens");
                        }

                        break;
                    case "content_block_start":
                        var block = root.GetProperty("content_block");
                        var opened = Str(block, "type") switch
                        {
                            "text" => new StreamedBlock(null, null),
                            "tool_use" => new StreamedBlock(
                                Str(block, "id") ?? string.Empty,
                                Str(block, "name") ?? string.Empty
                            ),
                            _ => null,
                        };

                        blocks[root.GetProperty("index").GetInt32()] = opened;

                        if (opened is { Name: null } && Str(block, "text") is { Length: > 0 } said)
                        {
                            opened.Pieces.Append(said);
                            text(said);
                        }

                        break;
                    case "content_block_delta":
                        if (
                            !blocks.TryGetValue(root.GetProperty("index").GetInt32(), out var into)
                            || into is null
                        )
                            break;

                        var delta = root.GetProperty("delta");

                        switch (Str(delta, "type"))
                        {
                            case "text_delta" when Str(delta, "text") is { Length: > 0 } piece:
                                into.Pieces.Append(piece);
                                text(piece);

                                break;
                            case "input_json_delta":
                                into.Pieces.Append(Str(delta, "partial_json"));

                                break;
                        }

                        break;
                    case "message_delta":
                        if (
                            root.TryGetProperty("delta", out var end)
                            && Str(end, "stop_reason") is { } reason
                        )
                            stop = reason;

                        if (root.TryGetProperty("usage", out var last))
                        {
                            output = Math.Max(output, Int(last, "output_tokens"));
                            input = Math.Max(input, InputTokens(last));
                        }

                        break;
                    case "message_stop":
                        ended = true;

                        break;
                    case "error":
                        var error = root.GetProperty("error");

                        throw DriverHttp.StreamError(
                            Str(error, "type") ?? "stream_error",
                            $"{Str(error, "type")}: {Str(error, "message")}",
                            endpoint
                        );
                }
            }
        }
        catch (Exception ex)
            when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new AiProviderException(
                "unreadable",
                "streamed something that is not a message",
                ex
            );
        }

        if (!ended)
            throw new AiProviderException("unreadable", "stopped answering part way");

        var parts = ImmutableArray.CreateBuilder<AiPart>();

        foreach (var block in blocks.Values)
            if (block is { Name: null, Pieces.Length: > 0 })
                parts.Add(new AiTextPart(block.Pieces.ToString()));
            else if (block is { Name: { } name })
                parts.Add(
                    new AiToolCallPart(block.Id!, name, DriverHttp.ParsePieces(block.Pieces))
                );

        return new AiDriverAnswer(
            new AiMessage(AiRole.Assistant, parts.ToImmutable()),
            Finish(stop),
            input,
            output,
            model
        );
    }

    private static AiFinish Finish(string? stop) =>
        stop switch
        {
            "tool_use" => AiFinish.ToolUse,
            "max_tokens" => AiFinish.MaxTokens,
            "refusal" => AiFinish.Refused,
            _ => AiFinish.EndTurn,
        };

    private static int InputTokens(JsonElement usage) =>
        Int(usage, "input_tokens")
        + Int(usage, "cache_creation_input_tokens")
        + Int(usage, "cache_read_input_tokens");

    /// <summary>A block of a streamed answer: text (no name), or a tool call and its arguments' pieces.</summary>
    private sealed record StreamedBlock(string? Id, string? Name)
    {
        public StringBuilder Pieces { get; } = new();
    }

    private static string? ReadError(string body) =>
        DriverHttp.ErrorMessage(
            body,
            root =>
                root.TryGetProperty("error", out var error)
                    ? $"{Str(error, "type")}: {Str(error, "message")}"
                    : null
        );

    private static int Int(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;

    private static string? Str(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) ? value.GetString() : null;
}
