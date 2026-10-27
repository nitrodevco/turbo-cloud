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

    public static HttpRequestMessage Build(AiEndpoint endpoint, AiProviderRequest request)
    {
        var url = string.IsNullOrWhiteSpace(endpoint.Url) ? DEFAULT_URL : endpoint.Url;
        var message = new HttpRequestMessage(HttpMethod.Post, $"{url.TrimEnd('/')}/v1/messages")
        {
            Content = new StringContent(
                Body(request, endpoint).ToJsonString(),
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

    public static JsonObject Body(AiProviderRequest request, AiEndpoint? endpoint = null)
    {
        var body = new JsonObject
        {
            ["model"] = request.Model,
            ["max_tokens"] = request.MaxTokens,
        };

        if (!string.IsNullOrWhiteSpace(request.Instructions))
            body["system"] = request.Instructions;

        body["messages"] = Messages(request.Messages);

        if (request.Temperature is { } temperature)
            body["temperature"] = temperature;

        if (request.Tools.Length > 0)
        {
            body["tools"] = new JsonArray(
                [
                    .. request.Tools.Select(t => new JsonObject
                    {
                        ["name"] = t.Name,
                        ["description"] = t.Description,
                        ["input_schema"] = JsonNode.Parse(t.InputSchema.GetRawText()),
                    }),
                ]
            );

            if (request.RequireTool)
                body["tool_choice"] = new JsonObject { ["type"] = "any" };
        }

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
                                ["input"] = call.Arguments.ValueKind == JsonValueKind.Object
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

            var finish = root.TryGetProperty("stop_reason", out var stop)
                ? stop.GetString() switch
                {
                    "tool_use" => AiFinish.ToolUse,
                    "max_tokens" => AiFinish.MaxTokens,
                    "refusal" => AiFinish.Refused,
                    _ => AiFinish.EndTurn,
                }
                : AiFinish.EndTurn;
            var input = 0;
            var output = 0;

            if (root.TryGetProperty("usage", out var usage))
            {
                input =
                    Int(usage, "input_tokens")
                    + Int(usage, "cache_creation_input_tokens")
                    + Int(usage, "cache_read_input_tokens");
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
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new AiProviderException("unreadable", "answered something that is not a message", ex);
        }
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
