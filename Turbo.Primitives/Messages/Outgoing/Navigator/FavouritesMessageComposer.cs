using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record FavouritesMessageComposer : IComposer
{
    [Id(0)]
    public required int Limit { get; init; }

    [Id(1)]
    public required ImmutableArray<int> FavoriteRoomIds { get; init; }
}
