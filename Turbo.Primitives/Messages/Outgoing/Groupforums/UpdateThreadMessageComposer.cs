using Orleans;
using Turbo.Primitives.Guilds.Forums.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Groupforums;

/// <summary>A thread as it is now (asked for, locked, pinned, hidden or restored).</summary>
[GenerateSerializer, Immutable]
public sealed record UpdateThreadMessageComposer : IComposer
{
    [Id(0)]
    public required int GroupId { get; init; }

    [Id(1)]
    public required GuildForumThreadSnapshot Thread { get; init; }
}
