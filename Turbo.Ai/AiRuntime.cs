using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Ai.Approvals;
using Turbo.Ai.Configuration;
using Turbo.Ai.Drivers;
using Turbo.Ai.Storage;
using Turbo.Contracts.Ai;
using Turbo.Contracts.Plugins;
using Turbo.Contracts.Plugins.Mcp;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Ai;

/// <summary>
/// The agent loop (<c>docs/ai-sdk.md</c>): a prompt, then model answers and tool calls until the
/// model is done or a limit stops it. Tools are offered and run as the call's identity, with the
/// MCP tools' own schema checks; one that changes things waits on an approval; each answer goes
/// through the agent's middleware and the providers' failover; the conversation is kept when the
/// call names a memory.
/// </summary>
public sealed class AiRuntime(
    IOptionsMonitor<AiConfig> config,
    AiProviders providers,
    IAiStore store,
    IAiApprovals approvals,
    IAiPermissions permissions,
    AiRateLimits limits,
    TimeProvider clock,
    ILogger<AiRuntime> logger
)
{
    /// <summary>The tool an agent with an output type answers through.</summary>
    public const string RESPOND = "respond";

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Fields, so a run (a nested class) reaches them.
    private readonly AiProviders _providers = providers;
    private readonly IAiStore _store = store;
    private readonly IAiApprovals _approvals = approvals;
    private readonly IAiPermissions _permissions = permissions;
    private readonly TimeProvider _clock = clock;
    private readonly ILogger<AiRuntime> _logger = logger;

    public AiConfig Config => config.CurrentValue;

    public AiProviders Providers => _providers;

    public IAiStore Store => _store;

    /// <summary>Whether the plugin's calls can reach a model now.</summary>
    public AiAvailability AvailabilityFor(PluginManifest plugin)
    {
        if (plugin.Ai is null)
            return AiAvailability.NotDeclared;

        if (!Config.Plugins.Contains(plugin.Key, StringComparer.OrdinalIgnoreCase))
            return AiAvailability.NotAllowed;

        return _providers.AnyUsable ? AiAvailability.Ready : AiAvailability.NoProvider;
    }

    /// <summary>Runs <paramref name="agent"/> on <paramref name="prompt"/> for the plugin.</summary>
    public async Task<AiResponse> RunAsync(
        PluginManifest plugin,
        AiAgent agent,
        AiPrompt prompt,
        AiCall call,
        CancellationToken ct
    )
    {
        if (Refuse(plugin) is { } refused)
            return refused;

        var current = Config;
        var payer = call.Payer;

        if (
            !limits.TryStart(
                ($"plugin:{plugin.Key}", current.PluginRunsPerMinute),
                (payer is { } p ? $"player:{p.Value}" : "player:none", payer is null ? 0 : current.PlayerRunsPerMinute)
            )
        )
            return Stopped(
                AiStopReason.RateLimited,
                payer is null
                    ? "This plugin has started too many runs this minute."
                    : "Too many questions this minute; wait a moment and ask again."
            );

        var run = await AiRun.StartAsync(this, plugin, agent, call, ct).ConfigureAwait(false);

        if (run is null)
            return Stopped(AiStopReason.Unavailable, "That conversation is not this caller's.");

        run.Prompt(prompt);

        return await run.LoopAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Goes on with the run waiting in <paramref name="call"/>'s conversation, by
    /// <paramref name="decision"/> or else the approvals' answer.
    /// </summary>
    public async Task<AiResponse> ResumeAsync(
        PluginManifest plugin,
        AiAgent agent,
        AiCall call,
        AiApprovalDecision? decision,
        CancellationToken ct
    )
    {
        if (Refuse(plugin) is { } refused)
            return refused;

        if (call.Memory is null)
            return Stopped(AiStopReason.Unavailable, "Name the conversation to resume (AiCall.Memory).");

        var run = await AiRun.StartAsync(this, plugin, agent, call, ct).ConfigureAwait(false);

        if (run is null)
            return Stopped(AiStopReason.Unavailable, "That conversation is not this caller's.");

        if (run.Waiting is not { } waiting)
            return Stopped(AiStopReason.Unavailable, "That conversation waits on no approval.");

        decision ??= await _approvals.GetAsync(waiting.ApprovalId, ct).ConfigureAwait(false);

        if (decision is null)
            return run.StillWaiting();

        return await run.ResumeAsync(decision, ct).ConfigureAwait(false);
    }

    /// <summary>Deletes the conversation, if it is <paramref name="memory"/>'s owner's.</summary>
    public async Task<bool> ForgetAsync(
        PluginManifest plugin,
        AiAgent agent,
        AiMemory memory,
        CancellationToken ct
    )
    {
        var found = memory.ConversationId is { } id
            ? await _store.FindAsync(id, ct).ConfigureAwait(false)
            : await _store.LatestAsync(plugin.Key, agent.Name, memory.Participant, ct).ConfigureAwait(false);

        return found is not null
            && Owns(found, plugin, agent, memory)
            && await _store.DeleteAsync(found.Id, ct).ConfigureAwait(false);
    }

    internal static bool Owns(AiConversationRecord found, PluginManifest plugin, AiAgent agent, AiMemory memory) =>
        string.Equals(found.Plugin, plugin.Key, StringComparison.OrdinalIgnoreCase)
        && found.Agent == agent.Name
        && found.Participant == memory.Participant;

    private AiResponse? Refuse(PluginManifest plugin) =>
        AvailabilityFor(plugin) switch
        {
            AiAvailability.NotDeclared => Stopped(
                AiStopReason.Unavailable,
                $"The plugin {plugin.Key} does not declare \"ai\" in its manifest."
            ),
            AiAvailability.NotAllowed => Stopped(
                AiStopReason.Unavailable,
                $"The hotel has not allowed the plugin {plugin.Key} to use AI (Turbo:Ai:Plugins)."
            ),
            AiAvailability.NoProvider => Stopped(
                AiStopReason.Unavailable,
                "The hotel has no AI provider configured."
            ),
            _ => null,
        };

    internal static AiResponse Stopped(AiStopReason reason, string message) =>
        new() { Stop = reason, Message = message };

    /// <summary>The waiting part of a run, kept in its conversation while an approval is decided.</summary>
    internal sealed record PendingRun(
        string ApprovalId,
        ImmutableArray<AiToolCallPart> Calls,
        int? ActorPlayer,
        ImmutableArray<string> ActorNodes,
        int? Payer
    );

    /// <summary>One run: its conversation, tools, limits and what it has used.</summary>
    private sealed class AiRun
    {
        private readonly AiRuntime _rt;
        private readonly PluginManifest _plugin;
        private readonly AiAgent _agent;
        private readonly AiCall _call;
        private readonly AiActor _actor;
        private readonly PlayerId? _payer;
        private readonly AiConversationRecord? _conversation;
        private readonly List<AiMessage> _messages;
        private readonly List<AiToolCall> _toolCalls = [];
        private readonly HashSet<string> _called = new(StringComparer.Ordinal);
        private Dictionary<string, McpToolDefinition>? _offered;
        private McpToolDefinition? _respond;
        private IRespondTool? _respondTool;
        private AiUsage _usage = AiUsage.None;
        private int _steps;
        private string _text = string.Empty;
        private string? _provider;
        private string? _model;
        private object? _output;
        private AiConversationRecord? _record;

        private AiRun(
            AiRuntime rt,
            PluginManifest plugin,
            AiAgent agent,
            AiCall call,
            AiConversationRecord? conversation,
            PendingRun? waiting
        )
        {
            _rt = rt;
            _plugin = plugin;
            _agent = agent;
            _call = call;
            _conversation = conversation;
            _record = conversation;
            Waiting = waiting;
            _messages = conversation is null
                ? []
                : JsonSerializer.Deserialize<List<AiMessage>>(conversation.Messages, Json) ?? [];

            // A resumed run acts as, and counts against, whom the waiting one did.
            _actor = waiting is null
                ? call.As
                : waiting.ActorPlayer is { } player
                    ? AiActor.AsPlayer(player)
                    : AiActor.System([.. waiting.ActorNodes]);
            _payer = waiting is null ? call.Payer : waiting.Payer;
        }

        public PendingRun? Waiting { get; private set; }

        public static async Task<AiRun?> StartAsync(
            AiRuntime rt,
            PluginManifest plugin,
            AiAgent agent,
            AiCall call,
            CancellationToken ct
        )
        {
            AiConversationRecord? conversation = null;

            if (call.Memory is { } memory)
            {
                if (memory.ConversationId is { } id)
                {
                    conversation = await rt._store.FindAsync(id, ct).ConfigureAwait(false);

                    if (conversation is null || !Owns(conversation, plugin, agent, memory))
                        return null;
                }
                else if (!memory.Fresh)
                    conversation = await rt
                        ._store.LatestAsync(plugin.Key, agent.Name, memory.Participant, ct)
                        .ConfigureAwait(false);

                if (conversation is null)
                {
                    var now = rt._clock.GetUtcNow().UtcDateTime;

                    conversation = new AiConversationRecord
                    {
                        Plugin = plugin.Key,
                        Agent = agent.Name,
                        AgentType = agent.GetType().FullName,
                        Participant = memory.Participant,
                        Messages = "[]",
                        CreatedAt = now,
                        UpdatedAt = now,
                    };
                    await rt._store.SaveAsync(conversation, ct).ConfigureAwait(false);
                }
            }

            var waiting = conversation?.Pending is { } pending
                ? JsonSerializer.Deserialize<PendingRun>(pending, Json)
                : null;

            return new AiRun(rt, plugin, agent, call, conversation, waiting);
        }

        /// <summary>Adds the prompt; a call that waited on an approval and was not decided is answered as not run.</summary>
        public void Prompt(AiPrompt prompt)
        {
            Trim();

            if (Waiting is { } waiting)
            {
                _messages.Add(
                    new AiMessage(
                        AiRole.User,
                        [
                            .. waiting.Calls.Select(c => new AiToolResultPart(
                                c.Id,
                                c.Name,
                                "Not run: the conversation went on before a staff member approved it.",
                                true
                            )),
                        ]
                    )
                );
                Waiting = null;
            }

            var text = prompt.Text ?? string.Empty;

            if (prompt.PlayerWritten is { } written)
                text = (text.Length > 0 ? text + "\n\n" : string.Empty)
                    + JsonSerializer.Serialize(written);

            _messages.Add(AiMessage.User(text));
        }

        public AiResponse StillWaiting() =>
            Finish(
                AiStopReason.AwaitingApproval,
                "The tool still waits on a staff member's approval.",
                Waiting
            );

        public async Task<AiResponse> ResumeAsync(AiApprovalDecision decision, CancellationToken ct)
        {
            var waiting = Waiting!;

            Waiting = null;
            await BuildToolsAsync(ct).ConfigureAwait(false);

            var paused = await RunCallsAsync(waiting.Calls, decision, ct).ConfigureAwait(false);

            if (paused is not null)
                return await PauseAsync(paused.Value, ct).ConfigureAwait(false);

            var response = await LoopAsync(ct, skipTools: true).ConfigureAwait(false);

            return response;
        }

        public async Task<AiResponse> LoopAsync(CancellationToken ct, bool skipTools = false)
        {
            try
            {
                if (!skipTools || _offered is null)
                    await BuildToolsAsync(ct).ConfigureAwait(false);

                var instructions = Instructions();
                var maxSteps = Math.Max(1, Math.Min(_agent.MaxSteps, _rt.Config.MaxSteps));
                var budget = _call.Budget ?? _agent.Budget;
                var stops = _agent.StopWhen.ToList();
                var middleware = _agent.Middleware.ToList();

                while (true)
                {
                    if (ct.IsCancellationRequested)
                        return await EndAsync(AiStopReason.Cancelled, "The call was cancelled.", ct).ConfigureAwait(false);

                    if (_steps >= maxSteps)
                        return await EndAsync(AiStopReason.MaxSteps, $"It took the most steps ({maxSteps}).", ct).ConfigureAwait(false);

                    if (budget is { Tokens: { } tokens } && _usage.Tokens >= tokens)
                        return await EndAsync(AiStopReason.Budget, $"It used its {tokens} tokens.", ct).ConfigureAwait(false);

                    if (budget is { Cost: { } cost } && _usage.Cost >= cost)
                        return await EndAsync(AiStopReason.Budget, $"It spent its budget of {cost}.", ct).ConfigureAwait(false);

                    if (await DailyLimitReachedAsync(ct).ConfigureAwait(false))
                        return await EndAsync(
                            AiStopReason.DailyBudget,
                            "That's all the questions for today; come back tomorrow.",
                            ct
                        ).ConfigureAwait(false);

                    _steps++;

                    var step = new AiStep
                    {
                        Number = _steps,
                        Plugin = _plugin.Key,
                        Agent = _agent.Name,
                        Instructions = instructions,
                        Messages = [.. _messages],
                        Tools = Specs(),
                        RequireTool = _respond is not null,
                        MaxTokens = _agent.MaxTokens,
                        Temperature = _agent.Temperature,
                        Model = _call.Model ?? _agent.Model,
                        Providers = _call.Provider is { } only ? [only] : [.. _agent.Providers],
                        Timeout = _call.Timeout ?? _agent.Timeout,
                        Used = _usage,
                    };
                    AiStep? sent = null;
                    var usage = new AiUsageContext(_plugin.Key, _agent.Name, _payer?.Value, _record?.Id);
                    AiNext next = (s, c) =>
                    {
                        sent = s;

                        return _rt._providers.SendAsync(s, usage, c);
                    };

                    for (var i = middleware.Count - 1; i >= 0; i--)
                    {
                        var inner = next;
                        var mw = middleware[i];

                        next = (s, c) => mw.HandleAsync(s, inner, c);
                    }

                    AiAnswer answer;

                    try
                    {
                        answer = await next(step, ct).ConfigureAwait(false);
                    }
                    catch (AiProviderException ex)
                    {
                        _steps--;

                        return await EndAsync(AiStopReason.Failed, $"No provider could answer: {ex.Message}.", ct).ConfigureAwait(false);
                    }

                    // What was sent is what is remembered: middleware that trimmed or summarised
                    // the conversation keeps it so.
                    if (sent is { } changed && !changed.Messages.SequenceEqual(step.Messages))
                    {
                        _messages.Clear();
                        _messages.AddRange(changed.Messages);
                    }

                    _messages.Add(answer.Message);
                    _usage += answer.Usage;
                    _provider = answer.Provider;
                    _model = answer.Model;

                    if (answer.Message.Text is { Length: > 0 } said)
                        _text = said;

                    var calls = answer.Message.Parts.OfType<AiToolCallPart>().ToImmutableArray();

                    if (calls.Length > 0)
                    {
                        var paused = await RunCallsAsync(calls, null, ct).ConfigureAwait(false);

                        if (paused is not null)
                            return await PauseAsync(paused.Value, ct).ConfigureAwait(false);

                        if (_output is not null)
                            return await EndAsync(AiStopReason.Finished, null, ct).ConfigureAwait(false);
                    }
                    else if (answer.Finish == AiFinish.MaxTokens)
                        return await EndAsync(AiStopReason.Truncated, "The answer was cut at the most tokens.", ct).ConfigureAwait(false);
                    else if (_respond is not null)
                        _messages.Add(AiMessage.User($"Answer by calling the tool \"{RESPOND}\" with your answer."));
                    else
                        return await EndAsync(
                            AiStopReason.Finished,
                            answer.Finish == AiFinish.Refused ? "The model refused." : null,
                            ct
                        ).ConfigureAwait(false);

                    var state = new AiRunState(_steps, _usage, _called);

                    if (stops.FirstOrDefault(x => x.ShouldStop(state)) is { } stop)
                        return await EndAsync(AiStopReason.StopCondition, stop.Reason, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return await EndAsync(AiStopReason.Cancelled, "The call was cancelled.", CancellationToken.None).ConfigureAwait(false);
            }
        }

        private async Task<bool> DailyLimitReachedAsync(CancellationToken ct)
        {
            var limit = _rt.Config.PlayerDailyTokens;

            if (limit <= 0 || _payer is not { } payer)
                return false;

            var today = _rt._clock.GetUtcNow().UtcDateTime.Date;

            return await _rt._store.TokensSinceAsync(payer.Value, today, ct).ConfigureAwait(false) >= limit;
        }

        private string Instructions()
        {
            var text = _agent.Instructions.Trim() + "\n\n" + PlayerText.NOTE;

            if (_respond is not null)
                text += $"\n\nWhen you have the answer, call the tool \"{RESPOND}\" with it: that is how you answer.";

            return text;
        }

        private ImmutableArray<AiToolSpec> Specs() =>
        [
            .. _offered!.Values.Select(t => new AiToolSpec(t.Name, t.Description, t.InputSchema)),
            .. _respond is { } respond
                ? [new AiToolSpec(respond.Name, respond.Description, respond.InputSchema)]
                : Array.Empty<AiToolSpec>(),
        ];

        /// <summary>The agent's tools its identity may call, and the answer tool for an output type.</summary>
        private async Task BuildToolsAsync(CancellationToken ct)
        {
            var offered = new Dictionary<string, McpToolDefinition>(StringComparer.Ordinal);
            var caller = _actor.ToCaller();

            foreach (var tool in _agent.Tools)
            {
                var definition = McpToolDefinition.Create(tool, _plugin.Key);

                if (definition.Name == RESPOND && _agent.OutputType is not null)
                    throw new InvalidOperationException(
                        $"The agent {_agent.Name} has a tool named {RESPOND}: an agent with an output type answers through a tool of that name."
                    );

                if (!offered.TryAdd(definition.Name, definition))
                    throw new InvalidOperationException(
                        $"The agent {_agent.Name} has two tools named {definition.Name}."
                    );
            }

            foreach (var (name, definition) in offered.ToList())
                if (!await definition.IsAvailableToAsync(caller, HoldsAsync, ct).ConfigureAwait(false))
                    offered.Remove(name);

            _offered = offered;

            if (_agent.OutputType is { } output)
            {
                _respondTool = (IRespondTool)Activator.CreateInstance(
                    typeof(RespondTool<>).MakeGenericType(output)
                )!;
                _respond = McpToolDefinition.Create(_respondTool, _plugin.Key);
            }
        }

        private Task<bool> HoldsAsync(string node, CancellationToken ct)
        {
            if (_actor.Player is { } player)
                return _rt._permissions.HoldsAsync(player, node, ct);

            return Task.FromResult(_actor.Nodes.Contains(node) || _actor.Nodes.Contains("*"));
        }

        /// <summary>
        /// Runs the model's tool calls in order and adds their results; answers the calls still to
        /// run when one waits on an approval (<paramref name="decision"/> decides the first of them).
        /// </summary>
        private async Task<ImmutableArray<AiToolCallPart>?> RunCallsAsync(
            ImmutableArray<AiToolCallPart> calls,
            AiApprovalDecision? decision,
            CancellationToken ct
        )
        {
            var results = new List<AiPart>();

            for (var i = 0; i < calls.Length; i++)
            {
                var call = calls[i];

                _called.Add(call.Name);

                if (_respond is not null && call.Name == RESPOND)
                {
                    var answered = await _respond
                        .CallAsync(call.Arguments, Context(), ct)
                        .ConfigureAwait(false);

                    if (answered.Status == McpToolCallStatus.Ok && _output is null)
                    {
                        _output = _respondTool!.Answer;
                        results.Add(new AiToolResultPart(call.Id, call.Name, "Answer received."));
                    }
                    else
                        results.Add(Result(call, answered.Status, string.Join("\n", answered.Text)));

                    continue;
                }

                if (_output is not null)
                {
                    results.Add(Result(call, McpToolCallStatus.Error, "Not run: the answer was already given."));

                    continue;
                }

                if (!_offered!.TryGetValue(call.Name, out var tool))
                {
                    results.Add(
                        Result(call, McpToolCallStatus.Unavailable, string.Join("\n", McpToolCallResult.Unavailable(call.Name).Text))
                    );

                    continue;
                }

                var decided = i == 0 ? decision : null;

                if (tool.Destructive && decided is null)
                {
                    if (!_rt._approvals.Enabled)
                    {
                        results.Add(
                            Denied(
                                call,
                                $"{call.Name} changes things, so it needs a staff member's approval, and this hotel takes none: it was not run. Tell whoever asked that a staff member must do it."
                            )
                        );

                        continue;
                    }

                    AddResults(results);

                    return calls[i..];
                }

                if (decided is { Approved: false })
                {
                    results.Add(
                        Denied(
                            call,
                            $"A staff member did not approve {call.Name}, so it was not run{(string.IsNullOrWhiteSpace(decided.Note) ? "" : $" ({decided.Note})")}. Tell whoever asked."
                        )
                    );

                    continue;
                }

                results.Add(await RunToolAsync(tool, call, ct).ConfigureAwait(false));
            }

            AddResults(results);

            return null;
        }

        private void AddResults(List<AiPart> results)
        {
            if (results.Count > 0)
                _messages.Add(new AiMessage(AiRole.User, [.. results]));
        }

        private async Task<AiPart> RunToolAsync(McpToolDefinition tool, AiToolCallPart call, CancellationToken ct)
        {
            try
            {
                var result = await tool.CallAsync(call.Arguments, Context(), ct).ConfigureAwait(false);

                return Result(call, result.Status, string.Join("\n", result.Text));
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _rt._logger.LogError(
                    ex,
                    "The plugin {Key}'s tool {Tool} failed for its agent {Agent}",
                    _plugin.Key,
                    call.Name,
                    _agent.Name
                );

                return Result(call, McpToolCallStatus.Failed, string.Join("\n", McpToolCallResult.Failed(call.Name).Text));
            }
        }

        private McpToolContext Context() => new(_actor.ToCaller(), HoldsAsync);

        private AiToolResultPart Result(AiToolCallPart call, McpToolCallStatus status, string text)
        {
            _toolCalls.Add(
                new AiToolCall(
                    call.Name,
                    call.Arguments,
                    status switch
                    {
                        McpToolCallStatus.Ok => AiToolOutcome.Ok,
                        McpToolCallStatus.Error => AiToolOutcome.Error,
                        McpToolCallStatus.InvalidArguments => AiToolOutcome.InvalidArguments,
                        McpToolCallStatus.Unavailable => AiToolOutcome.Unavailable,
                        _ => AiToolOutcome.Failed,
                    },
                    text
                )
            );

            return new AiToolResultPart(call.Id, call.Name, text, status != McpToolCallStatus.Ok);
        }

        private AiToolResultPart Denied(AiToolCallPart call, string text)
        {
            _toolCalls.Add(new AiToolCall(call.Name, call.Arguments, AiToolOutcome.Denied, text));

            return new AiToolResultPart(call.Id, call.Name, text, true);
        }

        /// <summary>Asks for the first call's approval and keeps the run in its conversation until it is decided.</summary>
        private async Task<AiResponse> PauseAsync(ImmutableArray<AiToolCallPart> calls, CancellationToken ct)
        {
            var first = calls[0];
            var tool = _offered![first.Name];

            if (_record is null)
            {
                var now = _rt._clock.GetUtcNow().UtcDateTime;

                // A run without a memory keeps one while it waits, so it can be resumed.
                _record = new AiConversationRecord
                {
                    Plugin = _plugin.Key,
                    Agent = _agent.Name,
                    AgentType = _agent.GetType().FullName,
                    Participant = $"run:{Guid.NewGuid():N}",
                    Messages = "[]",
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                await _rt._store.SaveAsync(_record, ct).ConfigureAwait(false);
            }

            var id = await _rt
                ._approvals.RequestAsync(
                    new AiApprovalRequest(
                        _plugin.Key,
                        _agent.Name,
                        first.Name,
                        $"{_plugin.Name}'s assistant wants to use {tool.Title}",
                        $"{tool.Name} {first.Arguments.GetRawText()}",
                        tool.Nodes,
                        _payer,
                        _record.Id
                    ),
                    ct
                )
                .ConfigureAwait(false);

            _toolCalls.Add(
                new AiToolCall(first.Name, first.Arguments, AiToolOutcome.AwaitingApproval, "Waiting on a staff member's approval.")
            );
            Waiting = new PendingRun(
                id,
                calls,
                _actor.Player?.Value,
                _actor.Nodes,
                _payer?.Value
            );

            return await EndAsync(
                AiStopReason.AwaitingApproval,
                $"{first.Name} waits on a staff member's approval.",
                ct
            ).ConfigureAwait(false);
        }

        private async Task<AiResponse> EndAsync(AiStopReason reason, string? message, CancellationToken ct)
        {
            if (_record is not null)
            {
                _record.Messages = JsonSerializer.Serialize(_messages, Json);
                _record.AgentType = _agent.GetType().FullName;
                _record.PendingApprovalId = Waiting?.ApprovalId;
                _record.Pending = Waiting is null ? null : JsonSerializer.Serialize(Waiting, Json);
                _record.UpdatedAt = _rt._clock.GetUtcNow().UtcDateTime;
                await _rt._store.SaveAsync(_record, CancellationToken.None).ConfigureAwait(false);
            }

            return Finish(reason, message, Waiting);
        }

        private AiResponse Finish(AiStopReason reason, string? message, PendingRun? waiting)
        {
            var pending = waiting is { } w
                ? new AiPendingApproval(w.ApprovalId, w.Calls[0].Name, w.Calls[0].Arguments)
                : null;
            return new AiResponse<object>
            {
                Text = _text,
                Stop = reason,
                Message = message,
                Usage = _usage,
                Steps = _steps,
                ToolCalls = [.. _toolCalls],
                ConversationId = _record?.Id,
                Provider = _provider,
                Model = _model,
                Pending = pending,
                Output = reason == AiStopReason.Finished ? _output : null,
            };
        }

        /// <summary>Keeps at most <c>Turbo:Ai:MemoryMessages</c>, from the start of a turn.</summary>
        private void Trim()
        {
            var max = Math.Max(2, _rt.Config.MemoryMessages);

            if (_messages.Count <= max)
                return;

            var start = _messages.Count - max;

            while (start < _messages.Count && !IsTurnStart(_messages[start]))
                start++;

            _messages.RemoveRange(0, start);
        }

        private static bool IsTurnStart(AiMessage message) =>
            message.Role == AiRole.User && !message.Parts.OfType<AiToolResultPart>().Any();
    }

    private interface IRespondTool : IMcpTool
    {
        object? Answer { get; }
    }

    /// <summary>The tool an agent with an output type answers through: its input is the output's schema.</summary>
    [RequiresPermission("ai.respond")]
    private sealed class RespondTool<TOutput> : McpTool<TOutput>, IRespondTool
        where TOutput : notnull
    {
        public object? Answer { get; private set; }

        public override string Name => RESPOND;

        public override string Description =>
            "Give your answer. Call it once, with every field, when you have the answer.";

        public override bool ReadOnly => true;

        public override Task<McpToolResult> HandleAsync(TOutput input, McpToolContext ctx, CancellationToken ct)
        {
            Answer = input;

            return Task.FromResult(McpToolResult.Text("Answer received."));
        }
    }
}
