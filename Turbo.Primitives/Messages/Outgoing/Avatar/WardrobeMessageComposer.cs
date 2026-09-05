using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Snapshots.Wardrobe;

namespace Turbo.Primitives.Messages.Outgoing.Avatar;

[GenerateSerializer, Immutable]
public sealed record WardrobeMessageComposer : IComposer
{
    [Id(0)]
    public required int State { get; init; }

    [Id(1)]
    public required List<OutfitDataSnapshot> Outfits { get; init; }
}
