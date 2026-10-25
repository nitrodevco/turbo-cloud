using Turbo.Primitives.Events;

namespace Turbo.Primitives.Players.Events;

/// <summary>
/// A player logged in to the hotel: their ticket was taken and the connection is theirs. Raised
/// for every login, one that replaces an earlier connection of theirs included (which
/// <see cref="PlayerOnlineChangedEvent"/> does not count), and never for a login that was refused.
/// </summary>
public sealed record PlayerLoggedInEvent : DomainEvent
{
    public required PlayerId PlayerId { get; init; }
}
