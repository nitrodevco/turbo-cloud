using Orleans;

namespace Turbo.Primitives.Moderation.Snapshots;

/// <summary>
/// One of the reporter's calls for help still waiting for a moderator, as
/// <c>CallForHelpPendingCallsMessageParser</c> reads it: the id and time as strings, then the
/// message, which <c>HabboHelp.onPendingCallsForHelp</c> shows in the pending request window.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record CfhPendingCallSnapshot
{
    [Id(0)]
    public required string CallId { get; init; }

    [Id(1)]
    public required string TimeStamp { get; init; }

    [Id(2)]
    public required string Message { get; init; }
}
