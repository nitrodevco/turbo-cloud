using Orleans;
using Turbo.Primitives.Guilds.Forums.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Groupforums;

/// <summary>The thread its author just started.</summary>
[GenerateSerializer, Immutable]
public sealed record PostThreadMessageComposer : IComposer
{
    [Id(0)]
    public required int GroupId { get; init; }

    [Id(1)]
    public required GuildForumThreadSnapshot Thread { get; init; }
}
