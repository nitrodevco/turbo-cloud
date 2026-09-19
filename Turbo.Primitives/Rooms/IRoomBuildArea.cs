using System.Collections.Generic;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Rooms;

/// <summary>
/// A furni logic that lets a player build on some tiles without rights in the room (a rented
/// space). The placement path asks every such furni and knows none of them.
/// </summary>
public interface IRoomBuildArea
{
    /// <summary>Whether this player may build on every one of these tiles because of this furni.</summary>
    public bool GrantsBuildRights(PlayerId playerId, IReadOnlyCollection<int> tileIds);
}
