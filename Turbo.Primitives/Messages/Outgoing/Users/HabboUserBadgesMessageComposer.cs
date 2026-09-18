using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Users;

[GenerateSerializer, Immutable]
public sealed record HabboUserBadgesMessageComposer : IComposer
{
    [Id(0)]
    public required PlayerId PlayerId { get; init; }

    [Id(1)]
    public required ImmutableArray<PlayerBadgeSnapshot> Badges { get; init; }
}
