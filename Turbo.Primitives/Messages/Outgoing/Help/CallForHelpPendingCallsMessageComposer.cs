using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Help;

/// <summary>
/// The reporter's calls for help still open, the answer to <c>GetPendingCallsForHelp</c>: none and
/// the client goes on to the report (<c>HabboHelp.onPendingCallsForHelp</c>).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record CallForHelpPendingCallsMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<CfhPendingCallSnapshot> Calls { get; init; }
}
