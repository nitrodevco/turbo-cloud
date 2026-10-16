using System.Collections.Generic;
using Turbo.Primitives.Players;

namespace Turbo.Catalog.Grains;

internal sealed class PlayerProductClaimLiveState
{
    public required PlayerId PlayerId { get; init; }

    /// <summary>The claim ids the player has taken.</summary>
    public HashSet<string> Claimed { get; } = [];
}
