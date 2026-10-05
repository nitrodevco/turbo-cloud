using Turbo.Primitives.Events;

namespace Turbo.Primitives.Players.Events;

/// <summary>
/// Raised when a player logs in to this server, or their connection to it goes. A login that
/// replaces the player's earlier connection is not a change and raises nothing. Published without
/// being awaited, so a handler cannot hold up a login.
/// </summary>
public sealed record PlayerOnlineChangedEvent : IEvent
{
    public required PlayerId PlayerId { get; init; }

    public required bool Online { get; init; }
}
