using System;
using System.Collections.Immutable;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Contracts.Ai;

/// <summary>
/// Runs around each model answer of an agent's run: change what is sent (trim the conversation,
/// add context), answer without the model, or look at what came back. Call <paramref name="next"/>
/// once to go on; return without calling it to answer yourself.
/// </summary>
public interface IAiMiddleware
{
    Task<AiAnswer> HandleAsync(AiStep step, AiNext next, CancellationToken ct);
}

/// <summary>The rest of the chain: later middleware, then the providers.</summary>
public delegate Task<AiAnswer> AiNext(AiStep step, CancellationToken ct);

/// <summary>What one model answer is asked with. Change it with <c>with</c> and pass it on.</summary>
public sealed record AiStep
{
    /// <summary>1 for the first answer of the run.</summary>
    public int Number { get; init; }

    /// <summary>The plugin running it.</summary>
    public required string Plugin { get; init; }

    /// <summary>The agent's name.</summary>
    public required string Agent { get; init; }

    /// <summary>The instructions (the system prompt).</summary>
    public required string Instructions { get; init; }

    /// <summary>The conversation so far, oldest first; the last message is the newest.</summary>
    public required ImmutableArray<AiMessage> Messages { get; init; }

    /// <summary>The tools offered this step, as the model sees them.</summary>
    public ImmutableArray<AiToolSpec> Tools { get; init; } = [];

    /// <summary>The model must call a tool (an agent with an output type answers through one).</summary>
    public bool RequireTool { get; init; }

    public int MaxTokens { get; init; }

    public double? Temperature { get; init; }

    /// <summary>The model for the first provider tried; null for each provider's own.</summary>
    public string? Model { get; init; }

    /// <summary>Providers to try in order (the hotel's names).</summary>
    public ImmutableArray<string> Providers { get; init; } = [];

    public TimeSpan? Timeout { get; init; }

    /// <summary>What the run has used before this step.</summary>
    public AiUsage Used { get; init; } = AiUsage.None;
}

/// <summary>A tool as a model is offered it: its name, description and input schema.</summary>
public sealed record AiToolSpec(string Name, string Description, JsonElement InputSchema);

/// <summary>How a model ended one answer.</summary>
public enum AiFinish
{
    /// <summary>It was done.</summary>
    EndTurn,

    /// <summary>It called tools and waits for their results.</summary>
    ToolUse,

    /// <summary>It hit the most tokens.</summary>
    MaxTokens,

    /// <summary>It refused, or the provider's filter stopped it.</summary>
    Refused,
}

/// <summary>One model answer: what it said (text and tool calls), why it ended, what it used, who answered.</summary>
public sealed record AiAnswer(
    AiMessage Message,
    AiFinish Finish,
    AiUsage Usage,
    string Provider,
    string Model
);
