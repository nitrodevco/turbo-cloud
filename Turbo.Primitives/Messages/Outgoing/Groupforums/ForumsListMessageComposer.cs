using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Guilds.Forums.Enums;
using Turbo.Primitives.Guilds.Forums.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Groupforums;

/// <summary>A page of a forum list.</summary>
[GenerateSerializer, Immutable]
public sealed record ForumsListMessageComposer : IComposer
{
    [Id(0)]
    public required GuildForumListType ListCode { get; init; }

    [Id(1)]
    public required int TotalAmount { get; init; }

    [Id(2)]
    public required int StartIndex { get; init; }

    [Id(3)]
    public required ImmutableArray<GuildForumSnapshot> Forums { get; init; }
}
