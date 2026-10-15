using System.Collections.Generic;
using Turbo.Database.Entities.Catalog;
using Turbo.Primitives.Players;

namespace Turbo.Catalog.Grains;

internal sealed class PlayerTargetedOfferLiveState
{
    public required PlayerId PlayerId { get; init; }

    /// <summary>The player's rows by offer id: the offers they moved the window of or bought.</summary>
    public Dictionary<int, PlayerTargetedOfferEntity> Rows { get; } = [];
}
