using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Ai;
using Turbo.Ai.Configuration;
using Turbo.Ai.Drivers;
using Turbo.Ai.Storage;
using Turbo.Contracts.Ai;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Ai;

/// <summary>
/// The two drivers' wire formats against handwritten fixtures of Anthropic's Messages API and
/// OpenAI's chat completions (a tool-use round each way, usage, errors), and the providers'
/// failover: the next provider is asked the same step when one fails, each attempt is logged, and
/// a key never reaches a message or the log.
/// </summary>
public sealed class AiProviderTests
{
    private const string KEY = "sk-ant-test-0123456789abcdef";

    private static readonly AiEndpoint ANTHROPIC = new(
        "claude",
        "",
        KEY,
        new Dictionary<string, string>(),
        new Dictionary<string, string> { ["top_k"] = "5", ["model"] = "\"not-this\"" }
    );

    private static readonly ImmutableArray<AiMessage> TOOL_ROUND =
    [
        AiMessage.User("Who is Alice?"),
        new(
            AiRole.Assistant,
            [
                new AiTextPart("Looking."),
                new AiToolCallPart("toolu_1", "find_player", JsonDocument.Parse("""{"name":"Al"}""").RootElement),
            ]
        ),
        new(AiRole.User, [new AiToolResultPart("toolu_1", "find_player", "{\"players\":[\"Alice\"]}")]),
        AiMessage.User("And Bob?"),
    ];

    private static readonly AiProviderRequest REQUEST = new(
        "claude-haiku-5-5",
        "You help staff.",
        TOOL_ROUND,
        [
            new AiToolSpec(
                "find_player",
                "Finds players.",
                JsonDocument.Parse("""{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}""").RootElement
            ),
        ],
        RequireTool: true,
        MaxTokens: 512,
        Temperature: 0.2
    );

    [Fact]
    public void Anthropic_request_is_the_messages_api_shape()
    {
        var body = AnthropicDriver.Body(REQUEST, ANTHROPIC).ToJsonString();

        SameJson(body, """{"model":"claude-haiku-5-5","max_tokens":512,"system":"You help staff.","messages":[{"role":"user","content":[{"type":"text","text":"Who is Alice?"}]},{"role":"assistant","content":[{"type":"text","text":"Looking."},{"type":"tool_use","id":"toolu_1","name":"find_player","input":{"name":"Al"}}]},{"role":"user","content":[{"type":"tool_result","tool_use_id":"toolu_1","content":"{\"players\":[\"Alice\"]}"},{"type":"text","text":"And Bob?"}]}],"temperature":0.2,"tools":[{"name":"find_player","description":"Finds players.","input_schema":{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}}],"tool_choice":{"type":"any"},"top_k":5}""");

        using var message = AnthropicDriver.Build(ANTHROPIC, REQUEST);

        message.RequestUri!.ToString().Should().Be("https://api.anthropic.com/v1/messages");
        message.Headers.GetValues("x-api-key").Single().Should().Be(KEY);
        message.Headers.GetValues("anthropic-version").Single().Should().Be("2023-06-01");
    }

    [Fact]
    public void Anthropic_answer_with_a_tool_call_is_read_with_its_usage()
    {
        var answer = AnthropicDriver.Read(
            """
            {"id":"msg_1","type":"message","role":"assistant","model":"claude-haiku-5-5",
             "content":[{"type":"text","text":"Let me look."},{"type":"tool_use","id":"toolu_2","name":"find_player","input":{"name":"Bob"}}],
             "stop_reason":"tool_use","usage":{"input_tokens":120,"cache_read_input_tokens":30,"output_tokens":40}}
            """
        );

        answer.Finish.Should().Be(AiFinish.ToolUse);
        answer.Message.Text.Should().Be("Let me look.");
        var call = answer.Message.Parts.OfType<AiToolCallPart>().Single();
        (call.Id, call.Name, call.Arguments.GetRawText()).Should().Be(("toolu_2", "find_player", """{"name":"Bob"}"""));
        (answer.InputTokens, answer.OutputTokens).Should().Be((150, 40));
    }

    [Fact]
    public void Openai_compatible_request_is_the_chat_completions_shape()
    {
        var local = new AiEndpoint("local", "http://localhost:11434/v1", null, new Dictionary<string, string>(), new Dictionary<string, string>());

        var body = OpenAiCompatibleDriver.Body(REQUEST with { RequireTool = false }, local).ToJsonString();

        SameJson(body, """{"model":"claude-haiku-5-5","messages":[{"role":"system","content":"You help staff."},{"role":"user","content":"Who is Alice?"},{"role":"assistant","content":"Looking.","tool_calls":[{"id":"toolu_1","type":"function","function":{"name":"find_player","arguments":"{\"name\":\"Al\"}"}}]},{"role":"tool","tool_call_id":"toolu_1","content":"{\"players\":[\"Alice\"]}"},{"role":"user","content":"And Bob?"}],"max_tokens":512,"temperature":0.2,"tools":[{"type":"function","function":{"name":"find_player","description":"Finds players.","parameters":{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}}}]}""");

        using var message = OpenAiCompatibleDriver.Build(local, REQUEST);

        message.RequestUri!.ToString().Should().Be("http://localhost:11434/v1/chat/completions");
        message.Headers.Authorization.Should().BeNull("a local server without a key gets none");
        OpenAiCompatibleDriver.Body(REQUEST).ToJsonString()
            .Should().Contain("\"max_completion_tokens\":512").And.Contain("\"tool_choice\":\"required\"");
    }

    [Fact]
    public void Openai_compatible_answer_reads_tool_calls_and_keeps_bad_arguments_as_text()
    {
        var answer = OpenAiCompatibleDriver.Read(
            """
            {"id":"chatcmpl-1","model":"llama3.1","choices":[{"index":0,"finish_reason":"stop","message":{"role":"assistant","content":null,
             "tool_calls":[{"id":"call_1","type":"function","function":{"name":"find_player","arguments":"{\"name\":\"Bob\"}"}},
                           {"id":"call_2","type":"function","function":{"name":"find_player","arguments":"name=Bob"}}]}}],
             "usage":{"prompt_tokens":90,"completion_tokens":12,"total_tokens":102}}
            """
        );

        answer.Finish.Should().Be(AiFinish.ToolUse, "calls decide, whatever finish_reason says");
        var calls = answer.Message.Parts.OfType<AiToolCallPart>().ToList();
        calls[0].Arguments.GetRawText().Should().Be("""{"name":"Bob"}""");
        calls[1].Arguments.ValueKind.Should().Be(JsonValueKind.String);
        (answer.InputTokens, answer.OutputTokens, answer.Model).Should().Be((90, 12, "llama3.1"));
    }

    [Fact]
    public async Task A_failing_provider_fails_over_to_the_next_and_each_attempt_is_logged_without_the_key()
    {
        var http = new ScriptedHttp(
            (_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)529)
            {
                Content = new StringContent(
                    $$$"""{"type":"error","error":{"type":"overloaded_error","message":"Overloaded for key {{{KEY}}}"}}"""
                ),
            }),
            (_, _) => Json(
                """{"model":"gpt-x","choices":[{"finish_reason":"stop","message":{"role":"assistant","content":"Hi!"}}],"usage":{"prompt_tokens":8,"completion_tokens":2}}"""
            )
        );
        var (providers, store, logs) = Providers(
            http,
            new AiConfig
            {
                Providers = new()
                {
                    ["claude"] = new() { Driver = "anthropic", Model = "claude-haiku-5-5", InputPricePerMillion = 1m },
                    ["openai"] = new() { Driver = "openai-compatible", Model = "gpt-x", InputPricePerMillion = 1_000_000m, OutputPricePerMillion = 0 },
                    ["unkeyed"] = new() { Driver = "openai-compatible", Model = "m" },
                },
                Keys = new() { ["claude"] = KEY, ["openai"] = "sk-proj-other0123456789" },
                Failover = ["unkeyed", "claude", "openai"],
            }
        );

        var answer = await providers.SendAsync(Step(), new AiUsageContext("p", "agent", 7, null), CancellationToken.None);

        answer.Provider.Should().Be("openai");
        answer.Message.Text.Should().Be("Hi!");
        answer.Usage.Cost.Should().Be(8m);
        http.Urls.Should().Equal("https://api.anthropic.com/v1/messages", "https://api.openai.com/v1/chat/completions");
        store.Usage.Select(u => (u.Provider, u.Outcome, u.PlayerId)).Should().Equal(("claude", "http_529", 7), ("openai", "ok", 7));
        logs.Entries.Should().ContainSingle().Which.Message.Should().Contain("overloaded_error").And.NotContain(KEY);
    }

    [Fact]
    public async Task When_every_provider_fails_the_failure_names_each_and_a_slow_one_times_out()
    {
        var http = new ScriptedHttp((_, _) => throw new HttpRequestException("refused"), async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);

            return await Json("{}");
        });
        var (providers, store, _) = Providers(
            http,
            new AiConfig
            {
                Providers = new()
                {
                    ["a"] = new() { Driver = "openai-compatible", Model = "m", NoKey = true, Url = "http://a/v1" },
                    ["b"] = new() { Driver = "openai-compatible", Model = "m", NoKey = true, Url = "http://b/v1", TimeoutSeconds = 1 },
                },
            }
        );

        var act = () => providers.SendAsync(Step(), new AiUsageContext("p", "agent", null, null), CancellationToken.None);

        (await act.Should().ThrowAsync<AiProviderException>())
            .Which.Message.Should().Contain("a: could not reach it").And.Contain("b: took longer than 1 s");
        store.Usage.Select(u => u.Outcome).Should().Equal("network", "timeout");
    }

    [Fact]
    public void Nothing_is_usable_without_a_key_unless_the_provider_takes_none()
    {
        var (providers, _, _) = Providers(
            new ScriptedHttp(),
            new AiConfig
            {
                Providers = new()
                {
                    ["claude"] = new() { Driver = "anthropic", Model = "m" },
                    ["odd"] = new() { Driver = "no-such-driver", Model = "m", NoKey = true },
                },
            }
        );

        providers.AnyUsable.Should().BeFalse();
    }

    private static void SameJson(string actual, string expected) =>
        JsonNode.DeepEquals(JsonNode.Parse(actual), JsonNode.Parse(expected)).Should().BeTrue(actual);

    private static AiStep Step() =>
        new()
        {
            Plugin = "p",
            Agent = "agent",
            Instructions = "Be brief.",
            Messages = [AiMessage.User("Hi")],
            MaxTokens = 64,
        };

    private static Task<HttpResponseMessage> Json(string body) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });

    private static (AiProviders, MemoryAiStore, CapturingLogger<AiProviders>) Providers(ScriptedHttp http, AiConfig config)
    {
        var store = new MemoryAiStore();
        var logs = new CapturingLogger<AiProviders>();
        var factory = new Factory(http);

        return (
            new AiProviders(
                new Monitor(config),
                [new AnthropicDriver(factory), new OpenAiCompatibleDriver(factory)],
                store,
                TimeProvider.System,
                logs
            ),
            store,
            logs
        );
    }

    private sealed class Monitor(AiConfig config) : IOptionsMonitor<AiConfig>
    {
        public AiConfig CurrentValue => config;

        public AiConfig Get(string? name) => config;

        public IDisposable? OnChange(Action<AiConfig, string?> listener) => null;
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    /// <summary>Answers each request with the next of its replies, keeping the addresses asked.</summary>
    private sealed class ScriptedHttp(params Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>[] replies)
        : HttpMessageHandler
    {
        private int _next;

        public List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Urls.Add(request.RequestUri!.ToString());

            return replies[_next++](request, ct);
        }
    }
}
