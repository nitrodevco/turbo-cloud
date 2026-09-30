using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.Events;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Primitives.Players.Events;

/// <summary>
/// Raised when what a player holds changes: a node or a meta value, whatever caused it (a write
/// to the player or one of their groups, an expiry, a reload, a plugin registering nodes). Not
/// raised when their permission grain activates, which is not a change. Published without being
/// awaited, so a handler may call back into the player's permission grain.
/// </summary>
public sealed record PlayerPermissionsChangedEvent : IEvent
{
    public required PlayerId PlayerId { get; init; }

    public required ResolvedPermissionsSnapshot Previous { get; init; }

    public required ResolvedPermissionsSnapshot Current { get; init; }

    /// <summary>Nodes held now and not before.</summary>
    public IEnumerable<string> Gained => Current.Granted.Except(Previous.Granted);

    /// <summary>Nodes held before and not now.</summary>
    public IEnumerable<string> Lost => Previous.Granted.Except(Current.Granted);
}
