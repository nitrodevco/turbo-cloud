using System.Collections.Generic;
using Turbo.Primitives.Players;

namespace Turbo.Catalog.Grains;

/// <summary>The Builders Club is one grain for the hotel, so its state carries no key.</summary>
internal sealed class BuildersClubLiveState
{
    /// <summary>How much furni each player is borrowing; a player with none is simply absent.</summary>
    public Dictionary<PlayerId, int> BorrowedCountByPlayerId { get; } = [];
}
