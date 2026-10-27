using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Turbo.Ai.Approvals;
using Turbo.Contracts.Ai;
using Turbo.Contracts.Plugins;
using Turbo.Plugins;
using Turbo.Primitives.Plugins;

namespace Turbo.Ai;

/// <summary>
/// Language models for the plugins (<see cref="IPluginAiHost"/>): each plugin's own
/// <see cref="IAi"/>, and the runs that waited on an approval, resumed with their plugin's agent
/// once it is decided (<see cref="DecidedAsync"/>).
/// </summary>
public sealed class AiHost(AiRuntime runtime, ILogger<AiHost> logger) : IPluginAiHost, IAiOverview
{
    private readonly ConcurrentDictionary<string, LivePlugin> _live = new(
        StringComparer.OrdinalIgnoreCase
    );

    public AiRuntime Runtime => runtime;

    // Every plugin is given its IAi as it loads: those declaring AI, for the panel.
    private readonly ConcurrentDictionary<string, PluginManifest> _declared = new(
        StringComparer.OrdinalIgnoreCase
    );

    public IAi ForPlugin(PluginManifest manifest)
    {
        if (manifest.Ai is not null)
            _declared[manifest.Key] = manifest;

        return new PluginAi(runtime, manifest);
    }

    public async Task<AiOverview> GetAsync(int days, CancellationToken ct)
    {
        var config = runtime.Config;
        var since = DateTime.UtcNow.Date.AddDays(-Math.Clamp(days, 1, 366) + 1);
        var totals = await runtime.Store.TotalsSinceAsync(since, ct).ConfigureAwait(false);

        return new AiOverview(
            [
                .. config
                    .Providers.OrderBy(x => x.Key, StringComparer.Ordinal)
                    .Select(x => new AiProviderView(
                        x.Key,
                        x.Value.Driver,
                        x.Value.Url,
                        x.Value.Model,
                        config.Keys.Keys.Contains(x.Key, StringComparer.OrdinalIgnoreCase),
                        runtime.Providers.Usable([x.Key]).Count > 0
                    )),
            ],
            [.. config.Failover],
            [
                .. _declared
                    .Values.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new AiPluginView(
                        x.Key,
                        x.Name,
                        x.Ai!.Purpose,
                        config.Plugins.Contains(x.Key, StringComparer.OrdinalIgnoreCase)
                    )),
            ],
            [
                .. totals.Select(x => new AiUsageView(
                    x.Plugin,
                    x.Provider,
                    x.Model,
                    x.Calls,
                    x.Failed,
                    x.InputTokens,
                    x.OutputTokens,
                    x.Cost
                )),
            ],
            since
        );
    }

    public IDisposable Live(PluginManifest manifest, Assembly assembly, IServiceProvider services)
    {
        var live = new LivePlugin(manifest, assembly, services);

        _live[manifest.Key] = live;

        return new Removal(this, manifest.Key, live);
    }

    /// <summary>
    /// An approval was decided: the run waiting on it goes on with its plugin's agent, which is
    /// told the answer (<see cref="AiAgent.ResumedAsync"/>). The resumed run's answer; null when no run waits on it, or its
    /// plugin is not running (then <see cref="IAi.ResumeAsync"/> goes on with it later).
    /// </summary>
    public async Task<AiResponse?> DecidedAsync(
        string approvalId,
        AiApprovalDecision decision,
        CancellationToken ct
    )
    {
        var waiting = await runtime.Store.FindByApprovalAsync(approvalId, ct).ConfigureAwait(false);

        if (waiting is null)
            return null;

        if (
            !_live.TryGetValue(waiting.Plugin, out var live)
            || waiting.AgentType is not { } typeName
            || live.Assembly.GetType(typeName) is not { } type
            || !typeof(AiAgent).IsAssignableFrom(type)
        )
        {
            logger.LogInformation(
                "The approval {Approval} was decided, but the plugin {Plugin} that waits on it is not running",
                approvalId,
                waiting.Plugin
            );

            return null;
        }

        AiAgent agent;

        try
        {
            agent = (AiAgent)ActivatorUtilities.CreateInstance(live.Services, type);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(
                ex,
                "The approval {Approval} was decided, but the plugin {Plugin}'s agent {Agent} can't be built from its services to go on",
                approvalId,
                waiting.Plugin,
                type.Name
            );

            return null;
        }

        var payer = waiting.Pending is { } pending
            ? System
                .Text.Json.JsonSerializer.Deserialize<AiRuntime.PendingRun>(pending, AiRuntime.Json)
                ?.Payer
            : null;
        var response = await runtime
            .ResumeAsync(
                live.Manifest,
                agent,
                new AiCall { Memory = AiMemory.Of(waiting.Participant, waiting.Id) },
                decision,
                ct
            )
            .ConfigureAwait(false);

        try
        {
            await agent
                .ResumedAsync(
                    new AiResumed(
                        PluginAi.Plain(response),
                        payer is { } player ? new Turbo.Primitives.Players.PlayerId(player) : null,
                        waiting.Participant
                    ),
                    ct
                )
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "The plugin {Plugin}'s agent {Agent} failed taking its resumed answer",
                waiting.Plugin,
                agent.Name
            );
        }

        return PluginAi.Plain(response);
    }

    private sealed record LivePlugin(
        PluginManifest Manifest,
        Assembly Assembly,
        IServiceProvider Services
    );

    private sealed class Removal(AiHost owner, string key, LivePlugin live) : IDisposable
    {
        public void Dispose()
        {
            if (owner._live.TryGetValue(key, out var current) && ReferenceEquals(current, live))
                owner._live.TryRemove(key, out _);
        }
    }
}

/// <summary>One plugin's <see cref="IAi"/>: its calls, by its manifest.</summary>
internal sealed class PluginAi(AiRuntime runtime, PluginManifest manifest) : IAi
{
    public AiAvailability Availability => runtime.AvailabilityFor(manifest);

    public async Task<AiResponse> PromptAsync(
        AiAgent agent,
        AiPrompt prompt,
        AiCall? call = null,
        CancellationToken ct = default
    ) =>
        Plain(
            await runtime
                .RunAsync(manifest, agent, prompt, call ?? new AiCall(), ct)
                .ConfigureAwait(false)
        );

    public async Task<AiResponse<TOutput>> PromptAsync<TOutput>(
        AiAgent<TOutput> agent,
        AiPrompt prompt,
        AiCall? call = null,
        CancellationToken ct = default
    )
        where TOutput : class =>
        Typed<TOutput>(
            await runtime
                .RunAsync(manifest, agent, prompt, call ?? new AiCall(), ct)
                .ConfigureAwait(false)
        );

    public Task<AiResponse> AskAsync(
        AiPrompt prompt,
        string? instructions = null,
        AiCall? call = null,
        CancellationToken ct = default
    ) => PromptAsync(new Anonymous(instructions), prompt, call, ct);

    public Task<AiResponse<TOutput>> AskAsync<TOutput>(
        AiPrompt prompt,
        string? instructions = null,
        AiCall? call = null,
        CancellationToken ct = default
    )
        where TOutput : class =>
        PromptAsync(new Anonymous<TOutput>(instructions), prompt, call, ct);

    public async Task<AiResponse> ResumeAsync(
        AiAgent agent,
        AiCall call,
        CancellationToken ct = default
    ) => Plain(await runtime.ResumeAsync(manifest, agent, call, null, ct).ConfigureAwait(false));

    public Task<bool> ForgetAsync(AiAgent agent, AiMemory memory, CancellationToken ct = default) =>
        runtime.ForgetAsync(manifest, agent, memory, ct);

    /// <summary>A response without the runtime's untyped output.</summary>
    internal static AiResponse Plain(AiResponse r) =>
        r.GetType() == typeof(AiResponse)
            ? r
            : new AiResponse
            {
                Text = r.Text,
                Stop = r.Stop,
                Message = r.Message,
                Usage = r.Usage,
                Steps = r.Steps,
                ToolCalls = r.ToolCalls,
                ConversationId = r.ConversationId,
                Provider = r.Provider,
                Model = r.Model,
                Pending = r.Pending,
            };

    internal static AiResponse<TOutput> Typed<TOutput>(AiResponse r)
        where TOutput : class =>
        new()
        {
            Text = r.Text,
            Stop = r.Stop,
            Message = r.Message,
            Usage = r.Usage,
            Steps = r.Steps,
            ToolCalls = r.ToolCalls,
            ConversationId = r.ConversationId,
            Provider = r.Provider,
            Model = r.Model,
            Pending = r.Pending,
            Output = (r as AiResponse<object>)?.Output as TOutput,
        };

    private const string ASK = "You answer one question, briefly and plainly.";

    /// <summary>An agent with no class: <see cref="IAi.AskAsync"/>'s.</summary>
    private sealed class Anonymous(string? instructions) : AiAgent
    {
        public override string Name => "ask";

        public override string Instructions => instructions ?? ASK;
    }

    private sealed class Anonymous<TOutput>(string? instructions) : AiAgent<TOutput>
        where TOutput : class
    {
        public override string Name => "ask";

        public override string Instructions => instructions ?? ASK;
    }
}
