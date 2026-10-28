using System.Text.Json;

namespace Turbo.Contracts.Ai;

/// <summary>
/// What a streamed run (<see cref="IAi.StreamAsync"/>) tells as it goes: the model's text a piece
/// at a time, each tool call as the model makes it and as it ends, and last the run's answer.
/// </summary>
public abstract record AiStreamEvent;

/// <summary>A piece of the text the model is writing in step <see cref="Step"/>; a step's pieces joined are its text.</summary>
public sealed record AiTextDelta(int Step, string Text) : AiStreamEvent;

/// <summary>The model called a tool in step <see cref="Step"/>; it has not run yet.</summary>
public sealed record AiToolCallStarted(int Step, string CallId, string Name, JsonElement Arguments)
    : AiStreamEvent;

/// <summary>A tool call ended: it ran, was refused, or waits on an approval (<see cref="AiToolCall.Outcome"/>).</summary>
public sealed record AiToolCallEnded(int Step, AiToolCall Call) : AiStreamEvent;

/// <summary>The run ended: the answer <see cref="IAi.PromptAsync"/> would have returned. Always the last event.</summary>
public sealed record AiStreamEnd(AiResponse Response) : AiStreamEvent;
