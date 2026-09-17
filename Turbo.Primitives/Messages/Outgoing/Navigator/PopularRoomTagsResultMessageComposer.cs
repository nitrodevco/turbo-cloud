using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record PopularRoomTagsResultMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<NavigatorPopularTagSnapshot> Tags { get; init; }
}
