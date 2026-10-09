using Orleans;
using Turbo.Primitives.Guilds.Forums.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Groupforums;

/// <summary>A forum, its settings and what the viewer may do in it.</summary>
[GenerateSerializer, Immutable]
public sealed record ForumDataMessageComposer : IComposer
{
    [Id(0)]
    public required GuildForumDetailSnapshot Forum { get; init; }
}
