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
                new AiToolCallPart(
                    "toolu_1",
                    "find_player",
                    JsonDocument.Parse("""{"name":"Al"}""").RootElement
                ),
            ]
        ),
        new(
            AiRole.User,
            [new AiToolResultPart("toolu_1", "find_player", "{\"players\":[\"Alice\"]}")]
        ),
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
                JsonDocument
                    .Parse(
                        """{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}"""
                    )
                    .RootElement
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

        SameJson(
            body,
            """{"model":"claude-haiku-5-5","max_tokens":512,"system":"You help staff.","messages":[{"role":"user","content":[{"type":"text","text":"Who is Alice?"}]},{"role":"assistant","content":[{"type":"text","text":"Looking."},{"type":"tool_use","id":"toolu_1","name":"find_player","input":{"name":"Al"}}]},{"role":"user","content":[{"type":"tool_result","tool_use_id":"toolu_1","content":"{\"players\":[\"Alice\"]}"},{"type":"text","text":"And Bob?"}]}],"temperature":0.2,"tools":[{"name":"find_player","description":"Finds players.","input_schema":{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}}],"tool_choice":{"type":"any"},"top_k":5}"""
        );

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
        (call.Id, call.Name, call.Arguments.GetRawText())
            .Should()
            .Be(("toolu_2", "find_player", """{"name":"Bob"}"""));
        (answer.InputTokens, answer.OutputTokens).Should().Be((150, 40));
    }

    [Fact]
    public async Task Anthropic_streamed_answer_gives_each_text_piece_and_joins_a_tool_calls_arguments()
    {
        string? sent = null;
        var http = new ScriptedHttp(
            async (request, ct) =>
            {
                sent = await request.Content!.ReadAsStringAsync(ct);

                return Sse(
                    """
                    event: message_start
                    data: {"type":"message_start","message":{"id":"msg_1","type":"message","role":"assistant","content":[],"model":"claude-haiku-5-5","stop_reason":null,"usage":{"input_tokens":120,"cache_read_input_tokens":30,"output_tokens":1}}}

                    event: content_block_start
                    data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

                    event: ping
                    data: {"type": "ping"}

                    event: content_block_delta
                    data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Let me"}}

                    event: content_block_delta
                    data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":" look."}}

                    event: content_block_stop
                    data: {"type":"content_block_stop","index":0}

                    event: content_block_start
                    data: {"type":"content_block_start","index":1,"content_block":{"type":"tool_use","id":"toolu_2","name":"find_player","input":{}}}

                    event: content_block_delta
                    data: {"type":"content_block_delta","index":1,"delta":{"type":"input_json_delta","partial_json":""}}

                    event: content_block_delta
                    data: {"type":"content_block_delta","index":1,"delta":{"type":"input_json_delta","partial_json":"{\"name\": \"Bo"}}

                    event: content_block_delta
                    data: {"type":"content_block_delta","index":1,"delta":{"type":"input_json_delta","partial_json":"b\"}"}}

                    event: content_block_stop
                    data: {"type":"content_block_stop","index":1}

                    event: message_delta
                    data: {"type":"message_delta","delta":{"stop_reason":"tool_use","stop_sequence":null},"usage":{"output_tokens":40}}

                    event: message_stop
                    data: {"type":"message_stop"}


                    """
                );
            }
        );
        var pieces = new List<string>();

        var answer = await new AnthropicDriver(new Factory(http)).StreamAsync(
            ANTHROPIC,
            REQUEST,
            pieces.Add,
            CancellationToken.None
        );

        JsonNode.Parse(sent!)!["stream"]!.GetValue<bool>().Should().BeTrue();
        pieces.Should().Equal("Let me", " look.");
        answer.Message.Text.Should().Be("Let me look.");
        var call = answer.Message.Parts.OfType<AiToolCallPart>().Single();
        (call.Id, call.Name, call.Arguments.GetProperty("name").GetString())
            .Should()
            .Be(("toolu_2", "find_player", "Bob"));
        (answer.Finish, answer.InputTokens, answer.OutputTokens, answer.Model)
            .Should()
            .Be((AiFinish.ToolUse, 150, 40, "claude-haiku-5-5"));
    }

    [Fact]
    public async Task Anthropic_error_event_part_way_fails_the_answer_by_its_type()
    {
        var http = new ScriptedHttp(
            (_, _) =>
                Task.FromResult(
                    Sse(
                        """
                        event: message_start
                        data: {"type":"message_start","message":{"model":"claude-haiku-5-5","usage":{"input_tokens":5,"output_tokens":1}}}

                        event: content_block_start
                        data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

                        event: content_block_delta
                        data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hel"}}

                        event: error
                        data: {"type":"error","error":{"type":"overloaded_error","message":"Overloaded"}}


                        """
                    )
                )
        );

        var failing = () =>
            new AnthropicDriver(new Factory(http)).StreamAsync(
                ANTHROPIC,
                REQUEST,
                _ => { },
                CancellationToken.None
            );

        (await failing.Should().ThrowAsync<AiProviderException>())
            .Which.Kind.Should()
            .Be("overloaded_error");
    }

    [Fact]
    public async Task Openai_compatible_streamed_answer_gives_each_text_piece_and_joins_a_tool_calls_arguments()
    {
        string? sent = null;
        var http = new ScriptedHttp(
            async (request, ct) =>
            {
                sent = await request.Content!.ReadAsStringAsync(ct);

                return Sse(
                    """
                    data: {"id":"c1","object":"chat.completion.chunk","model":"gpt-x","choices":[{"index":0,"delta":{"role":"assistant","content":""},"finish_reason":null}]}

                    data: {"id":"c1","object":"chat.completion.chunk","model":"gpt-x","choices":[{"index":0,"delta":{"content":"Looking"},"finish_reason":null}]}

                    data: {"id":"c1","object":"chat.completion.chunk","model":"gpt-x","choices":[{"index":0,"delta":{"content":" now."},"finish_reason":null}]}

                    data: {"id":"c1","object":"chat.completion.chunk","model":"gpt-x","choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"id":"call_9","type":"function","function":{"name":"find_player","arguments":""}}]},"finish_reason":null}]}

                    data: {"id":"c1","object":"chat.completion.chunk","model":"gpt-x","choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"function":{"arguments":"{\"name\":"}}]},"finish_reason":null}]}

                    data: {"id":"c1","object":"chat.completion.chunk","model":"gpt-x","choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"function":{"arguments":"\"Bob\"}"}}]},"finish_reason":null}]}

                    data: {"id":"c1","object":"chat.completion.chunk","model":"gpt-x","choices":[{"index":0,"delta":{},"finish_reason":"tool_calls"}]}

                    data: {"id":"c1","object":"chat.completion.chunk","model":"gpt-x","choices":[],"usage":{"prompt_tokens":8,"completion_tokens":12,"total_tokens":20}}

                    data: [DONE]


                    """
                );
            }
        );
        var pieces = new List<string>();

        var answer = await new OpenAiCompatibleDriver(new Factory(http)).StreamAsync(
            new AiEndpoint(
                "openai",
                "",
                "sk-proj-other0123456789",
                new Dictionary<string, string>(),
                new Dictionary<string, string>()
            ),
            REQUEST,
            pieces.Add,
            CancellationToken.None
        );

        var body = JsonNode.Parse(sent!)!;
        body["stream"]!.GetValue<bool>().Should().BeTrue();
        body["stream_options"]!["include_usage"]!.GetValue<bool>().Should().BeTrue();
        pieces.Should().Equal("Looking", " now.");
        answer.Message.Text.Should().Be("Looking now.");
        var call = answer.Message.Parts.OfType<AiToolCallPart>().Single();
        (call.Id, call.Name, call.Arguments.GetProperty("name").GetString())
            .Should()
            .Be(("call_9", "find_player", "Bob"));
        (answer.Finish, answer.InputTokens, answer.OutputTokens, answer.Model)
            .Should()
            .Be((AiFinish.ToolUse, 8, 12, "gpt-x"));
    }

    [Fact]
    public async Task A_streamed_step_fails_over_only_until_its_first_piece_of_text()
    {
        const string CUT = """
            data: {"model":"gpt-x","choices":[{"index":0,"delta":{"content":"Hal"},"finish_reason":null}]}


            """;
        const string WHOLE = """
            data: {"model":"gpt-x","choices":[{"index":0,"delta":{"content":"Hi!"},"finish_reason":"stop"}]}

            data: [DONE]


            """;
        var config = new AiConfig
        {
            Providers = new()
            {
                ["first"] = new()
                {
                    Driver = "openai-compatible",
                    Model = "a",
                    NoKey = true,
                },
                ["second"] = new()
                {
                    Driver = "openai-compatible",
                    Model = "b",
                    NoKey = true,
                },
            },
            Failover = ["first", "second"],
        };
        var before = new ScriptedHttp(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)),
            (_, _) => Task.FromResult(Sse(WHOLE))
        );
        var after = new ScriptedHttp((_, _) => Task.FromResult(Sse(CUT)));
        var pieces = new List<string>();

        var answer = await Providers(before, config)
            .Item1.StreamAsync(
                Step(),
                new AiUsageContext("p", "agent", null, null),
                pieces.Add,
                CancellationToken.None
            );
        var failing = () =>
            Providers(after, config)
                .Item1.StreamAsync(
                    Step(),
                    new AiUsageContext("p", "agent", null, null),
                    _ => { },
                    CancellationToken.None
                );

        (answer.Provider, answer.Message.Text).Should().Be(("second", "Hi!"));
        pieces.Should().Equal("Hi!");
        (await failing.Should().ThrowAsync<AiProviderException>())
            .Which.Message.Should()
            .Contain("first: stopped answering part way");
        after.Urls.Should().ContainSingle("the second is not asked once the first has begun");
    }

    [Fact]
    public async Task The_connection_test_asks_for_one_token_and_reports_a_refusal_without_the_key()
    {
        string? sent = null;
        var http = new ScriptedHttp(
            async (request, ct) =>
            {
                sent = await request.Content!.ReadAsStringAsync(ct);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"model":"claude-haiku-5-5","content":[{"type":"text","text":"OK"}],"stop_reason":"max_tokens","usage":{"input_tokens":9,"output_tokens":1}}"""
                    ),
                };
            },
            (_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.Unauthorized)
                    {
                        Content = new StringContent(
                            $$$"""{"type":"error","error":{"type":"authentication_error","message":"invalid x-api-key {{{KEY}}}"}}"""
                        ),
                    }
                )
        );
        var (providers, store, _) = Providers(
            http,
            new AiConfig
            {
                Providers = new()
                {
                    ["claude"] = new() { Driver = "anthropic", Model = "claude-haiku-5-5" },
                    ["unkeyed"] = new() { Driver = "openai-compatible", Model = "m" },
                },
                Keys = new() { ["claude"] = KEY },
            }
        );

        var ok = await providers.TestAsync("Claude", CancellationToken.None);
        var refused = await providers.TestAsync("claude", CancellationToken.None);
        var unkeyed = await providers.TestAsync("unkeyed", CancellationToken.None);

        JsonNode.Parse(sent!)!["max_tokens"]!.GetValue<int>().Should().Be(1);
        (ok.Ok, ok.Model, ok.InputTokens, ok.OutputTokens)
            .Should()
            .Be((true, "claude-haiku-5-5", 9, 1));
        refused.Ok.Should().BeFalse();
        refused
            .Message.Should()
            .Contain("401")
            .And.Contain("authentication_error")
            .And.NotContain(KEY);
        (unkeyed.Ok, unkeyed.Message)
            .Should()
            .Be((false, "It has no key: enter one, or mark it as taking none."));
        http.Urls.Should().HaveCount(2, "a provider without a key is not asked");
        store
            .Usage.Select(u => (u.Plugin, u.Provider, u.Outcome))
            .Should()
            .Equal(("(panel)", "claude", "ok"), ("(panel)", "claude", "http_401"));
    }

    private static HttpResponseMessage Sse(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "text/event-stream"),
        };

    [Fact]
    public void Openai_compatible_request_is_the_chat_completions_shape()
    {
        var local = new AiEndpoint(
            "local",
            "http://localhost:11434/v1",
            null,
            new Dictionary<string, string>(),
            new Dictionary<string, string>()
        );

        var body = OpenAiCompatibleDriver
            .Body(REQUEST with { RequireTool = false }, local)
            .ToJsonString();

        SameJson(
            body,
            """{"model":"claude-haiku-5-5","messages":[{"role":"system","content":"You help staff."},{"role":"user","content":"Who is Alice?"},{"role":"assistant","content":"Looking.","tool_calls":[{"id":"toolu_1","type":"function","function":{"name":"find_player","arguments":"{\"name\":\"Al\"}"}}]},{"role":"tool","tool_call_id":"toolu_1","content":"{\"players\":[\"Alice\"]}"},{"role":"user","content":"And Bob?"}],"max_tokens":512,"temperature":0.2,"tools":[{"type":"function","function":{"name":"find_player","description":"Finds players.","parameters":{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}}}]}"""
        );

        using var message = OpenAiCompatibleDriver.Build(local, REQUEST);

        message.RequestUri!.ToString().Should().Be("http://localhost:11434/v1/chat/completions");
        message.Headers.Authorization.Should().BeNull("a local server without a key gets none");
        OpenAiCompatibleDriver
            .Body(REQUEST)
            .ToJsonString()
            .Should()
            .Contain("\"max_completion_tokens\":512")
            .And.Contain("\"tool_choice\":\"required\"");
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
            (_, _) =>
                Task.FromResult(
                    new HttpResponseMessage((HttpStatusCode)529)
                    {
                        Content = new StringContent(
                            $$$"""{"type":"error","error":{"type":"overloaded_error","message":"Overloaded for key {{{KEY}}}"}}"""
                        ),
                    }
                ),
            (_, _) =>
                Json(
                    """{"model":"gpt-x","choices":[{"finish_reason":"stop","message":{"role":"assistant","content":"Hi!"}}],"usage":{"prompt_tokens":8,"completion_tokens":2}}"""
                )
        );
        var (providers, store, logs) = Providers(
            http,
            new AiConfig
            {
                Providers = new()
                {
                    ["claude"] = new()
                    {
                        Driver = "anthropic",
                        Model = "claude-haiku-5-5",
                        InputPricePerMillion = 1m,
                    },
                    ["openai"] = new()
                    {
                        Driver = "openai-compatible",
                        Model = "gpt-x",
                        InputPricePerMillion = 1_000_000m,
                        OutputPricePerMillion = 0,
                    },
                    ["unkeyed"] = new() { Driver = "openai-compatible", Model = "m" },
                },
                Keys = new() { ["claude"] = KEY, ["openai"] = "sk-proj-other0123456789" },
                Failover = ["unkeyed", "claude", "openai"],
            }
        );

        var answer = await providers.SendAsync(
            Step(),
            new AiUsageContext("p", "agent", 7, null),
            CancellationToken.None
        );

        answer.Provider.Should().Be("openai");
        answer.Message.Text.Should().Be("Hi!");
        answer.Usage.Cost.Should().Be(8m);
        http.Urls.Should()
            .Equal(
                "https://api.anthropic.com/v1/messages",
                "https://api.openai.com/v1/chat/completions"
            );
        store
            .Usage.Select(u => (u.Provider, u.Outcome, u.PlayerId))
            .Should()
            .Equal(("claude", "http_529", 7), ("openai", "ok", 7));
        logs.Entries.Should()
            .ContainSingle()
            .Which.Message.Should()
            .Contain("overloaded_error")
            .And.NotContain(KEY);
    }

    [Fact]
    public async Task When_every_provider_fails_the_failure_names_each_and_a_slow_one_times_out()
    {
        var http = new ScriptedHttp(
            (_, _) => throw new HttpRequestException("refused"),
            async (_, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);

                return await Json("{}");
            }
        );
        var (providers, store, _) = Providers(
            http,
            new AiConfig
            {
                Providers = new()
                {
                    ["a"] = new()
                    {
                        Driver = "openai-compatible",
                        Model = "m",
                        NoKey = true,
                        Url = "http://a/v1",
                    },
                    ["b"] = new()
                    {
                        Driver = "openai-compatible",
                        Model = "m",
                        NoKey = true,
                        Url = "http://b/v1",
                        TimeoutSeconds = 1,
                    },
                },
            }
        );

        var act = () =>
            providers.SendAsync(
                Step(),
                new AiUsageContext("p", "agent", null, null),
                CancellationToken.None
            );

        (await act.Should().ThrowAsync<AiProviderException>())
            .Which.Message.Should()
            .Contain("a: could not reach it")
            .And.Contain("b: took longer than 1 s");
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
                    ["odd"] = new()
                    {
                        Driver = "no-such-driver",
                        Model = "m",
                        NoKey = true,
                    },
                },
            }
        );

        providers.AnyUsable.Should().BeFalse();
    }

    private static void SameJson(string actual, string expected) =>
        JsonNode
            .DeepEquals(JsonNode.Parse(actual), JsonNode.Parse(expected))
            .Should()
            .BeTrue(actual);

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
        Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }
        );

    private static (AiProviders, MemoryAiStore, CapturingLogger<AiProviders>) Providers(
        ScriptedHttp http,
        AiConfig config
    )
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
    private sealed class ScriptedHttp(
        params Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>[] replies
    ) : HttpMessageHandler
    {
        private int _next;

        public List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            Urls.Add(request.RequestUri!.ToString());

            return replies[_next++](request, ct);
        }
    }
}
