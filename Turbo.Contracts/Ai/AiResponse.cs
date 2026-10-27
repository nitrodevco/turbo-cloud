using System.Collections.Immutable;
using System.Text.Json;

namespace Turbo.Contracts.Ai;

/// <summary>Why a run ended.</summary>
public enum AiStopReason
{
    /// <summary>The model answered and was done.</summary>
    Finished,

    /// <summary>The model's answer was cut at the agent's <see cref="AiAgent.MaxTokens"/>.</summary>
    Truncated,

    /// <summary>It took the agent's (or the hotel's) most steps.</summary>
    MaxSteps,

    /// <summary>The run's budget (<see cref="AiBudget"/>) was spent.</summary>
    Budget,

    /// <summary>The player has used their tokens for today (<c>Turbo:Ai:PlayerDailyTokens</c>).</summary>
    DailyBudget,

    /// <summary>Too many calls a minute, by the player or the plugin; nothing was sent.</summary>
    RateLimited,

    /// <summary>One of the agent's <see cref="AiStopWhen"/> conditions.</summary>
    StopCondition,

    /// <summary>A tool waits on a staff member's approval; the run goes on once it is decided.</summary>
    AwaitingApproval,

    /// <summary>The caller's cancellation token was cancelled.</summary>
    Cancelled,

    /// <summary>No model can be asked (<see cref="AiAvailability"/>), or the conversation is not the caller's; nothing was sent.</summary>
    Unavailable,

    /// <summary>Every provider tried failed; <see cref="AiResponse.Message"/> says how, without a key or a body.</summary>
    Failed,
}

/// <summary>Tokens in and out, and their cost by the hotel's prices.</summary>
public sealed record AiUsage(int InputTokens = 0, int OutputTokens = 0, decimal Cost = 0)
{
    public static AiUsage None { get; } = new();

    public int Tokens => InputTokens + OutputTokens;

    public static AiUsage operator +(AiUsage a, AiUsage b) =>
        new(a.InputTokens + b.InputTokens, a.OutputTokens + b.OutputTokens, a.Cost + b.Cost);
}

/// <summary>A tool the model called in a run, and how it went.</summary>
public sealed record AiToolCall(
    string Name,
    JsonElement Arguments,
    AiToolOutcome Outcome,
    string Result
);

/// <summary>How a tool call went.</summary>
public enum AiToolOutcome
{
    Ok,

    /// <summary>The tool answered an error.</summary>
    Error,

    /// <summary>The arguments did not fit; the model was told each problem.</summary>
    InvalidArguments,

    /// <summary>No such tool for the identity the agent acts as.</summary>
    Unavailable,

    /// <summary>It threw; the error is in the server's log.</summary>
    Failed,

    /// <summary>It waits on an approval.</summary>
    AwaitingApproval,

    /// <summary>A staff member refused it; the model was told.</summary>
    Denied,
}

/// <summary>A tool call waiting on an approval: the approval's id, the tool and its arguments.</summary>
public sealed record AiPendingApproval(string ApprovalId, string Tool, JsonElement Arguments);

/// <summary>A run's answer.</summary>
public record AiResponse
{
    /// <summary>The model's last text.</summary>
    public string Text { get; init; } = string.Empty;

    public AiStopReason Stop { get; init; }

    /// <summary>Why it stopped, in words, when it did not finish.</summary>
    public string? Message { get; init; }

    /// <summary>What the run used, every step and provider together.</summary>
    public AiUsage Usage { get; init; } = AiUsage.None;

    /// <summary>The model answers it took.</summary>
    public int Steps { get; init; }

    /// <summary>The tools the model called, in order.</summary>
    public ImmutableArray<AiToolCall> ToolCalls { get; init; } = [];

    /// <summary>The conversation it was saved to (<see cref="AiMemory.Continue"/>); null without memory.</summary>
    public long? ConversationId { get; init; }

    /// <summary>The provider and model of the last answer.</summary>
    public string? Provider { get; init; }

    public string? Model { get; init; }

    /// <summary>The tool waiting on an approval (<see cref="AiStopReason.AwaitingApproval"/>).</summary>
    public AiPendingApproval? Pending { get; init; }

    /// <summary>The model finished.</summary>
    public bool Ok => Stop == AiStopReason.Finished;
}

/// <summary>A run's answer as <typeparamref name="TOutput"/>; null when it did not finish.</summary>
public sealed record AiResponse<TOutput> : AiResponse
    where TOutput : class
{
    public TOutput? Output { get; init; }
}
