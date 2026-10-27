using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Plugins.Mcp;

namespace Turbo.Contracts.Ai;

/// <summary>
/// An agent: a class holding what a model is told (<see cref="Instructions"/>), the tools it may
/// call, the shape of its answer and its limits. Run it with <see cref="IAi.PromptAsync"/>. Its
/// tools are MCP tools (<see cref="McpTool{TInput}"/>): the same class an assistant calls over MCP,
/// with the same schema, argument checks and permission node, checked against the identity the
/// agent acts as (<see cref="AiCall.As"/>).
/// </summary>
public abstract class AiAgent
{
    /// <summary>Its name in the usage log and its conversations; the class's name by default.</summary>
    public virtual string Name => GetType().Name;

    /// <summary>What the model is told before the conversation: its role, its rules, how to answer.</summary>
    public abstract string Instructions { get; }

    /// <summary>
    /// The tools it may call. Each is offered only when the identity the agent acts as holds one of
    /// its nodes; one that is not read-only and not marked <c>Destructive =&gt; false</c> waits on
    /// a staff member's approval before it runs (<c>docs/ai-sdk.md</c>, Approvals).
    /// </summary>
    public virtual IEnumerable<IMcpTool> Tools => [];

    /// <summary>The most model answers in one run (each tool round is one); the hotel caps it (<c>Turbo:Ai:MaxSteps</c>).</summary>
    public virtual int MaxSteps => 8;

    /// <summary>The most tokens one model answer may be.</summary>
    public virtual int MaxTokens => 1024;

    /// <summary>The sampling temperature (0 to 1); null for the provider's.</summary>
    public virtual double? Temperature => null;

    /// <summary>A model for the first provider tried; null for the provider's configured model.</summary>
    public virtual string? Model => null;

    /// <summary>The providers to try, in order, by the hotel's names; empty for the hotel's order (<c>Turbo:Ai:Failover</c>).</summary>
    public virtual IReadOnlyList<string> Providers => [];

    /// <summary>How long one model answer may take; null for the provider's timeout.</summary>
    public virtual TimeSpan? Timeout => null;

    /// <summary>What one run may spend at most; null for no limit but the hotel's.</summary>
    public virtual AiBudget? Budget => null;

    /// <summary>Middleware around each model answer, outermost first (<see cref="IAiMiddleware"/>).</summary>
    public virtual IEnumerable<IAiMiddleware> Middleware => [];

    /// <summary>Conditions that end a run early, checked after each answer (<see cref="AiStopWhen"/>).</summary>
    public virtual IEnumerable<AiStopWhen> StopWhen => [];

    /// <summary>The type it answers with (<see cref="AiAgent{TOutput}"/>); null for text.</summary>
    public virtual Type? OutputType => null;

    /// <summary>
    /// A run that waited on an approval went on once it was decided: say the answer to whoever
    /// asked (whisper it to the player, post it to the room). Called on the hotel's own thread,
    /// never inside a room's turn.
    /// </summary>
    public virtual Task ResumedAsync(AiResponse response, CancellationToken ct) =>
        Task.CompletedTask;
}

/// <summary>
/// An agent answering with <typeparamref name="TOutput"/>, a record whose properties are the
/// answer's fields (<c>[Description]</c>, <c>[Range]</c> and lengths go into its schema, as an MCP
/// tool's input does). An answer that does not fit is sent back to the model with each problem.
/// </summary>
public abstract class AiAgent<TOutput> : AiAgent
    where TOutput : class
{
    public sealed override Type? OutputType => typeof(TOutput);
}

/// <summary>What one run may spend: tokens (in and out) and the cost the hotel's prices give them.</summary>
public sealed record AiBudget(int? Tokens = null, decimal? Cost = null);

/// <summary>
/// A condition that ends a run after a model answer, with <see cref="AiStopReason.StopCondition"/>
/// and <see cref="Reason"/>.
/// </summary>
public sealed class AiStopWhen(string reason, Func<AiRunState, bool> stop)
{
    public string Reason { get; } = reason;

    public bool ShouldStop(AiRunState run) => stop(run);

    /// <summary>Once the model has called the tool <paramref name="name"/>.</summary>
    public static AiStopWhen ToolCalled(string name) =>
        new($"{name} was called", run => run.ToolsCalled.Contains(name));

    /// <summary>Once the run has used <paramref name="tokens"/> tokens or more.</summary>
    public static AiStopWhen TokensOver(int tokens) =>
        new($"over {tokens} tokens", run => run.Usage.Tokens >= tokens);

    /// <summary>Once <paramref name="when"/> says so.</summary>
    public static AiStopWhen When(string reason, Func<AiRunState, bool> when) => new(reason, when);
}

/// <summary>Where a run is: answers so far, what they used, and the tools called.</summary>
public sealed record AiRunState(int Steps, AiUsage Usage, IReadOnlySet<string> ToolsCalled);
