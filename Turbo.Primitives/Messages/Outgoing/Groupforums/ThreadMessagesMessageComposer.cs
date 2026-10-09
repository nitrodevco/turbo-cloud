using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Guilds.Forums.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Groupforums;

/// <summary>A page of a thread's messages.</summary>
[GenerateSerializer, Immutable]
public sealed record ThreadMessagesMessageComposer : IComposer
{
    [Id(0)]
    public required int GroupId { get; init; }

    [Id(1)]
    public required int ThreadId { get; init; }

    [Id(2)]
    public required int StartIndex { get; init; }

    [Id(3)]
    public required ImmutableArray<GuildForumMessageSnapshot> Messages { get; init; }
}
