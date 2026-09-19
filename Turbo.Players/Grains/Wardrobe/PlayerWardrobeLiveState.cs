using System.Collections.Generic;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots.Wardrobe;

namespace Turbo.Players.Grains.Wardrobe;

internal sealed class PlayerWardrobeLiveState
{
    public required PlayerId PlayerId { get; init; }
    public SortedDictionary<int, OutfitDataSnapshot> OutfitsBySlot { get; } = [];
}
