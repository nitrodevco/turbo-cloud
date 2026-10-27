using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Ai;
using Turbo.Ai.Approvals;
using Turbo.Ai.Configuration;
using Turbo.Ai.Drivers;
using Turbo.Ai.Storage;
using Turbo.Contracts.Ai;
using Turbo.Contracts.Plugins;
using Turbo.Plugins;
using Turbo.Primitives.Players;

namespace Turbo.Testing;

/// <summary>
/// Language models with no network, for plugin tests (<c>docs/ai-sdk.md</c>, Testing): the model's
/// answers are scripted (text, tool calls, a typed answer, a failure), and everything else is the
/// hotel's own: the agent loop, tools run with their schema checks and permission nodes, approvals,
/// budgets, rate limits and memory. A <see cref="TestHotel"/> has one (<c>hotel.Ai</c>); in a plain
/// test, <see cref="For"/> is a plugin's <see cref="IAi"/>.
/// </summary>
public sealed class AiFake : IPluginAiHost
{
    /// <summary>The provider name the fake answers as.</summary>
    public const string PROVIDER = "fake";

    private readonly ConcurrentQueue<Scripted> _script = new();
    private readonly List<AiProviderRequest> _requests = [];
    private readonly List<AiResponse> _responses = [];
    private readonly HashSet<string> _allowed = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _refused = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();
    private readonly AiHost _host;
    private int _calls;
    private bool _strict;

    public AiFake(FakeHotel? hotel = null, TimeProvider? clock = null)
    {
        Hotel = hotel;
        Clock = clock ?? hotel?.Clock ?? TimeProvider.System;
        Approvals = new FakeApprovals(this);

        var options = new Monitor(this);
        var providers = new AiProviders(
            options,
            [new Driver(this)],
            Store,
            Clock,
            NullLogger<AiProviders>.Instance
        );
        var runtime = new AiRuntime(
            options,
            providers,
            Store,
            Approvals,
            new Permissions(this),
            new AiRateLimits(Clock),
            Clock,
            NullLogger<AiRuntime>.Instance
        );

        _host = new AiHost(runtime, NullLogger<AiHost>.Instance);
    }

    /// <summary>Whose permission nodes a player-acting agent's tools are asked against (null: <see cref="Grant"/>'s).</summary>
    public FakeHotel? Hotel { get; }

    public TimeProvider Clock { get; }

    /// <summary>The hotel's AI settings in the test: daily tokens, rate limits, memory, steps. The fake provider and the plugins it hands out are added to them.</summary>
    public AiConfig Settings { get; set; } =
        new() { PlayerRunsPerMinute = 0, PluginRunsPerMinute = 0 };

    /// <summary>Conversations and the usage log, in memory.</summary>
    public MemoryAiStore Store { get; } = new();

    /// <summary>The approvals agents ask for: approve or deny them to resume their runs.</summary>
    public FakeApprovals Approvals { get; }

    /// <summary>What the model was asked, step by step, oldest first.</summary>
    public IReadOnlyList<AiProviderRequest> Requests
    {
        get
        {
            lock (_lock)
                return [.. _requests];
        }
    }

    /// <summary>Every run's answer, a resumed one's included, oldest first.</summary>
    public IReadOnlyList<AiResponse> Responses
    {
        get
        {
            lock (_lock)
                return [.. _responses];
        }
    }

    /// <summary>The usage log.</summary>
    public IReadOnlyList<AiUsageRecord> Usage => Store.Usage;

    /// <summary>The <see cref="IAi"/> of a plugin with this key, which declares AI and is allowed it.</summary>
    public IAi For(string pluginKey = "test-plugin") =>
        ForPlugin(
            new PluginManifest
            {
                Name = pluginKey,
                Key = pluginKey,
                Version = "1.0.0",
                Author = "test",
                Ai = new PluginAiUse { Purpose = "tests" },
            }
        );

    public IAi ForPlugin(PluginManifest manifest)
    {
        lock (_lock)
            _allowed.Add(manifest.Key);

        return new Recording(this, _host.ForPlugin(manifest));
    }

    public IDisposable Live(
        PluginManifest manifest,
        Assembly assembly,
        IServiceProvider services
    ) => _host.Live(manifest, assembly, services);

    /// <summary>The hotel's owner has not allowed the plugin (its calls answer <see cref="AiStopReason.Unavailable"/>).</summary>
    public AiFake Refuse(string pluginKey)
    {
        lock (_lock)
            _refused.Add(pluginKey);

        return this;
    }

    // --- The model's answers, in order ----------------------------------------------------------

    /// <summary>The model answers <paramref name="text"/> and is done.</summary>
    public AiFake Respond(string text, int inputTokens = 10, int outputTokens = 5) =>
        Then(_ => Answer([new AiTextPart(text)], AiFinish.EndTurn, inputTokens, outputTokens));

    /// <summary>The model calls the tool <paramref name="name"/> with <paramref name="arguments"/> (an object written in camelCase, or JSON).</summary>
    public AiFake CallTool(
        string name,
        object? arguments = null,
        int inputTokens = 10,
        int outputTokens = 5
    ) =>
        Then(_ =>
            Answer(
                [
                    new AiToolCallPart(
                        $"call_{Interlocked.Increment(ref _calls)}",
                        name,
                        Json(arguments)
                    ),
                ],
                AiFinish.ToolUse,
                inputTokens,
                outputTokens
            )
        );

    /// <summary>The model gives a typed agent's answer (it calls <c>respond</c> with <paramref name="output"/>).</summary>
    public AiFake Answer(object output, int inputTokens = 10, int outputTokens = 5) =>
        CallTool(AiRuntime.RESPOND, output, inputTokens, outputTokens);

    /// <summary>
    /// The model writes <paramref name="pieces"/> one at a time and is done: a streamed run
    /// (<see cref="IAi.StreamAsync"/>) is given each piece as it comes; any other gets them joined.
    /// </summary>
    public AiFake Stream(params string[] pieces)
    {
        _script.Enqueue(
            new Scripted(
                _ => Answer([new AiTextPart(string.Concat(pieces))], AiFinish.EndTurn, 10, 5),
                [.. pieces]
            )
        );

        return this;
    }

    /// <summary>The model's answer is cut at the most tokens.</summary>
    public AiFake Truncate(string text) =>
        Then(_ => Answer([new AiTextPart(text)], AiFinish.MaxTokens, 10, 5));

    /// <summary>The provider fails (<paramref name="kind"/>: <c>http_529</c>, <c>timeout</c>, ...).</summary>
    public AiFake Fail(string kind = "http_529") =>
        Then(_ => throw new AiProviderException(kind, $"answered {kind}"));

    /// <summary>The next answer is <paramref name="answer"/>'s, from what the model is asked.</summary>
    public AiFake Then(Func<AiProviderRequest, AiDriverAnswer> answer)
    {
        _script.Enqueue(new Scripted(answer, []));

        return this;
    }

    /// <summary>
    /// A call the script has no answer left for fails the test; without this it answers
    /// <c>"Fake answer."</c>.
    /// </summary>
    public AiFake PreventStrayPrompts()
    {
        _strict = true;

        return this;
    }

    /// <summary>A model answer for <see cref="Then"/>.</summary>
    public static AiDriverAnswer Answer(
        ImmutableArray<AiPart> parts,
        AiFinish finish,
        int inputTokens,
        int outputTokens
    ) =>
        new(
            new AiMessage(AiRole.Assistant, parts),
            finish,
            inputTokens,
            outputTokens,
            "fake-model"
        );

    // --- Assertions ----------------------------------------------------------------------------

    /// <summary>A prompt held <paramref name="text"/> (the plugin's words or a player's).</summary>
    public AiFake AssertPrompted(string text)
    {
        if (
            !Requests.Any(r =>
                r.Messages.Any(m =>
                    m.Role == AiRole.User && m.Text.Contains(text, StringComparison.Ordinal)
                )
            )
        )
            throw new HotelAssertionException(
                $"No prompt held \"{text}\". Prompted: {Describe(Requests.SelectMany(r => r.Messages).Where(m => m.Role == AiRole.User).Select(m => m.Text).Distinct())}."
            );

        return this;
    }

    /// <summary>No prompt held <paramref name="text"/>.</summary>
    public AiFake AssertNotPrompted(string text)
    {
        if (
            Requests.Any(r =>
                r.Messages.Any(m =>
                    m.Role == AiRole.User && m.Text.Contains(text, StringComparison.Ordinal)
                )
            )
        )
            throw new HotelAssertionException($"A prompt held \"{text}\".");

        return this;
    }

    /// <summary>The model was never asked.</summary>
    public AiFake AssertNeverPrompted()
    {
        if (Requests.Count > 0)
            throw new HotelAssertionException($"The model was asked {Requests.Count} time(s).");

        return this;
    }

    /// <summary>A run called the tool <paramref name="name"/> (with arguments <paramref name="arguments"/> accepts); answers the call.</summary>
    public AiToolCall AssertToolCalled(string name, Func<JsonElement, bool>? arguments = null)
    {
        var calls = Responses.SelectMany(r => r.ToolCalls).ToList();

        return calls.LastOrDefault(c =>
                c.Name == name && (arguments is null || arguments(c.Arguments))
            )
            ?? throw new HotelAssertionException(
                $"No run called {name}{(arguments is null ? "" : " with those arguments")}. Called: {Describe(calls.Select(c => $"{c.Name} {c.Arguments.GetRawText()} ({c.Outcome})"))}."
            );
    }

    /// <summary>The tool <paramref name="name"/> never ran (it may have been asked for, refused or waiting).</summary>
    public AiFake AssertToolNotRun(string name)
    {
        if (
            Responses
                .SelectMany(r => r.ToolCalls)
                .Any(c => c.Name == name && c.Outcome == AiToolOutcome.Ok)
        )
            throw new HotelAssertionException($"{name} ran.");

        return this;
    }

    /// <summary>The last run stopped for <paramref name="reason"/>.</summary>
    public AiResponse AssertStoppedBy(AiStopReason reason)
    {
        var last =
            Responses.LastOrDefault() ?? throw new HotelAssertionException("No run has ended.");

        return last.Stop == reason
            ? last
            : throw new HotelAssertionException(
                $"The last run stopped by {last.Stop} ({last.Message}), not {reason}."
            );
    }

    private static string Describe(IEnumerable<string> items)
    {
        var list = items.ToList();

        return list.Count == 0 ? "none" : string.Join("; ", list.Select(x => $"\"{x}\""));
    }

    private static JsonElement Json(object? value) =>
        value switch
        {
            null => JsonSerializer.SerializeToElement(new { }),
            JsonElement element => element,
            string text => JsonDocument.Parse(text).RootElement.Clone(),
            _ => JsonSerializer.SerializeToElement(
                value,
                value.GetType(),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
            ),
        };

    private (AiDriverAnswer Answer, ImmutableArray<string> Pieces) Next(AiProviderRequest request)
    {
        lock (_lock)
            _requests.Add(request);

        if (_script.TryDequeue(out var scripted))
            return (scripted.Answer(request), scripted.Pieces);

        if (_strict)
            throw new HotelAssertionException(
                $"The model was asked with no scripted answer left (PreventStrayPrompts). Its last message: \"{request.Messages.LastOrDefault()?.Text}\"."
            );

        return (Answer([new AiTextPart("Fake answer.")], AiFinish.EndTurn, 10, 5), []);
    }

    /// <summary>A scripted answer, and the pieces a streamed run is given its text in (none: whole).</summary>
    private sealed record Scripted(
        Func<AiProviderRequest, AiDriverAnswer> Answer,
        ImmutableArray<string> Pieces
    );

    private void Record(AiResponse response)
    {
        lock (_lock)
            _responses.Add(response);
    }

    /// <summary>The approvals of the fake hotel: each request kept; <see cref="ApproveAsync"/> or <see cref="DenyAsync"/> resumes its run.</summary>
    public sealed class FakeApprovals(AiFake fake) : IAiApprovals
    {
        private readonly ConcurrentDictionary<string, AiApprovalDecision> _decided = new();
        private readonly List<(string Id, AiApprovalRequest Request)> _requests = [];
        private int _next;

        /// <summary>Whether the hotel takes approvals; off, an agent's tool that changes things is never run.</summary>
        public bool Enabled { get; set; } = true;

        public IReadOnlyList<(string Id, AiApprovalRequest Request)> Requests
        {
            get
            {
                lock (_requests)
                    return [.. _requests];
            }
        }

        public Task<string> RequestAsync(AiApprovalRequest request, CancellationToken ct)
        {
            var id = $"approval-{Interlocked.Increment(ref _next)}";

            lock (_requests)
                _requests.Add((id, request));

            return Task.FromResult(id);
        }

        public Task<AiApprovalDecision?> GetAsync(string id, CancellationToken ct) =>
            Task.FromResult(_decided.TryGetValue(id, out var d) ? d : null);

        /// <summary>Approves the latest request (or <paramref name="id"/>); answers the resumed run's response.</summary>
        public Task<AiResponse?> ApproveAsync(string? id = null, PlayerId? by = null) =>
            DecideAsync(id, new AiApprovalDecision(true, by));

        /// <summary>Refuses the latest request (or <paramref name="id"/>), with <paramref name="note"/> for the model.</summary>
        public Task<AiResponse?> DenyAsync(
            string? id = null,
            string? note = null,
            PlayerId? by = null
        ) => DecideAsync(id, new AiApprovalDecision(false, by, note));

        private async Task<AiResponse?> DecideAsync(string? id, AiApprovalDecision decision)
        {
            id ??=
                Requests.LastOrDefault().Id
                ?? throw new HotelAssertionException("No approval was asked for.");
            _decided[id] = decision;

            var resumed = await fake
                ._host.DecidedAsync(id, decision, CancellationToken.None)
                .ConfigureAwait(false);

            if (resumed is not null)
                fake.Record(resumed);

            return resumed;
        }
    }

    private sealed class Recording(AiFake fake, IAi inner) : IAi
    {
        public AiAvailability Availability => inner.Availability;

        public async Task<AiResponse> PromptAsync(
            AiAgent agent,
            AiPrompt prompt,
            AiCall? call = null,
            CancellationToken ct = default
        ) => Keep(await inner.PromptAsync(agent, prompt, call, ct).ConfigureAwait(false));

        public async Task<AiResponse<TOutput>> PromptAsync<TOutput>(
            AiAgent<TOutput> agent,
            AiPrompt prompt,
            AiCall? call = null,
            CancellationToken ct = default
        )
            where TOutput : class =>
            Keep(await inner.PromptAsync(agent, prompt, call, ct).ConfigureAwait(false));

        public async IAsyncEnumerable<AiStreamEvent> StreamAsync(
            AiAgent agent,
            AiPrompt prompt,
            AiCall? call = null,
            [EnumeratorCancellation] CancellationToken ct = default
        )
        {
            await foreach (
                var e in inner.StreamAsync(agent, prompt, call, ct).ConfigureAwait(false)
            )
            {
                if (e is AiStreamEnd end)
                    Keep(end.Response);

                yield return e;
            }
        }

        public async Task<AiResponse> AskAsync(
            AiPrompt prompt,
            string? instructions = null,
            AiCall? call = null,
            CancellationToken ct = default
        ) => Keep(await inner.AskAsync(prompt, instructions, call, ct).ConfigureAwait(false));

        public async Task<AiResponse<TOutput>> AskAsync<TOutput>(
            AiPrompt prompt,
            string? instructions = null,
            AiCall? call = null,
            CancellationToken ct = default
        )
            where TOutput : class =>
            Keep(
                await inner.AskAsync<TOutput>(prompt, instructions, call, ct).ConfigureAwait(false)
            );

        public async Task<AiResponse> ResumeAsync(
            AiAgent agent,
            AiCall call,
            CancellationToken ct = default
        ) => Keep(await inner.ResumeAsync(agent, call, ct).ConfigureAwait(false));

        public Task<bool> ForgetAsync(
            AiAgent agent,
            AiMemory memory,
            CancellationToken ct = default
        ) => inner.ForgetAsync(agent, memory, ct);

        private T Keep<T>(T response)
            where T : AiResponse
        {
            fake.Record(response);

            return response;
        }
    }

    private sealed class Driver(AiFake fake) : IAiDriver
    {
        public string Name => PROVIDER;

        public Task<AiDriverAnswer> SendAsync(
            AiEndpoint endpoint,
            AiProviderRequest request,
            CancellationToken ct
        )
        {
            ct.ThrowIfCancellationRequested();

            return Task.FromResult(fake.Next(request).Answer);
        }

        public Task<AiDriverAnswer> StreamAsync(
            AiEndpoint endpoint,
            AiProviderRequest request,
            Action<string> text,
            CancellationToken ct
        )
        {
            ct.ThrowIfCancellationRequested();

            var (answer, pieces) = fake.Next(request);

            if (pieces.Length > 0)
                foreach (var piece in pieces)
                    text(piece);
            else if (answer.Message.Text is { Length: > 0 } whole)
                text(whole);

            return Task.FromResult(answer);
        }
    }

    private sealed class Permissions(AiFake fake) : IAiPermissions
    {
        public Task<bool> HoldsAsync(PlayerId player, string node, CancellationToken ct) =>
            fake.Hotel is { } hotel
                ? hotel.Players.HasPermissionAsync(player, node, ct)
                : Task.FromResult(
                    fake._grants.TryGetValue(player, out var nodes)
                        && (nodes.Contains(node) || nodes.Contains("*"))
                );
    }

    private readonly ConcurrentDictionary<PlayerId, HashSet<string>> _grants = new();

    /// <summary>With no fake hotel: the player holds <paramref name="nodes"/> (<c>*</c> all of them).</summary>
    public AiFake Grant(PlayerId player, params string[] nodes)
    {
        _grants.GetOrAdd(player, _ => new HashSet<string>(StringComparer.Ordinal)).UnionWith(nodes);

        return this;
    }

    private sealed class Monitor(AiFake fake) : IOptionsMonitor<AiConfig>
    {
        public AiConfig CurrentValue
        {
            get
            {
                var s = fake.Settings;
                List<string> allowed;

                lock (fake._lock)
                    allowed =
                    [
                        .. s
                            .Plugins.Concat(fake._allowed)
                            .Where(x => !fake._refused.Contains(x))
                            .Distinct(),
                    ];

                var providers = new Dictionary<string, AiProviderConfig>(s.Providers);

                providers.TryAdd(
                    PROVIDER,
                    new AiProviderConfig
                    {
                        Driver = PROVIDER,
                        Model = "fake-model",
                        NoKey = true,
                    }
                );

                return new AiConfig
                {
                    Providers = providers,
                    Keys = s.Keys,
                    Failover = s.Failover,
                    Plugins = allowed,
                    PlayerDailyTokens = s.PlayerDailyTokens,
                    PlayerRunsPerMinute = s.PlayerRunsPerMinute,
                    PluginRunsPerMinute = s.PluginRunsPerMinute,
                    MaxSteps = s.MaxSteps,
                    MemoryMessages = s.MemoryMessages,
                    TimeoutSeconds = s.TimeoutSeconds,
                };
            }
        }

        public AiConfig Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<AiConfig, string?> listener) => null;
    }
}
