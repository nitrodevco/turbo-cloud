using System.Collections.Generic;
using Turbo.Database.Entities.Habbicons;
using Turbo.Primitives.Players;

namespace Turbo.Catalog.Grains;

internal sealed class PlayerHabbiconLiveState
{
    public required PlayerId PlayerId { get; init; }

    /// <summary>The player's rows by Habbicon id: claimable, owned or favourited.</summary>
    public Dictionary<int, PlayerHabbiconEntity> Rows { get; } = [];
}
