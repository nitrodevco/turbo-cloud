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

    public static HttpRequestMessage Build(AiEndpoint endpoint, AiProviderRequest request)
    {
        var url = string.IsNullOrWhiteSpace(endpoint.Url) ? DEFAULT_URL : endpoint.Url;
        var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"{url.TrimEnd('/')}/chat/completions"
        )
        {
            Content = new StringContent(
                Body(request, endpoint).ToJsonString(),
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

    public static JsonObject Body(AiProviderRequest request, AiEndpoint? endpoint = null)
    {
        var messages = new JsonArray();

        if (!string.IsNullOrWhiteSpace(request.Instructions))
            messages.Add(new JsonObject { ["role"] = "system", ["content"] = request.Instructions });

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
            body["tools"] = new JsonArray(
                [
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
                ]
            );

            if (request.RequireTool)
                body["tool_choice"] = "required";
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
            assistant["tool_calls"] = new JsonArray(
                [
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
                ]
            );

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

            // Some servers say "stop" with tool calls: the calls decide.
            var finish = parts.OfType<AiToolCallPart>().Any()
                ? AiFinish.ToolUse
                : (choice.TryGetProperty("finish_reason", out var reason) ? reason.GetString() : null)
                    switch
                {
                    "length" => AiFinish.MaxTokens,
                    "content_filter" => AiFinish.Refused,
                    _ => AiFinish.EndTurn,
                };
            var input = 0;
            var output = 0;

            if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
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
            when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            throw new AiProviderException("unreadable", "answered something that is not a chat completion", ex);
        }
    }

    private static string? ReadError(string body) =>
        DriverHttp.ErrorMessage(
            body,
            root =>
                root.TryGetProperty("error", out var error)
                    ? error.ValueKind == JsonValueKind.Object
                        ? error.TryGetProperty("message", out var m) ? m.GetString() : null
                        : error.GetString()
                    : null
        );

    private static int Int(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;
}
