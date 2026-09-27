using System.Collections.Generic;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Players;

namespace Turbo.Inventory.Grains.Unseen;

/// <summary>What a player's unseen-items grain holds: the new ids of each inventory tab.</summary>
internal sealed class PlayerUnseenItemsLiveState
{
    public required PlayerId PlayerId { get; init; }

    public Dictionary<UnseenItemCategory, HashSet<int>> IdsByCategory { get; } = [];
}
