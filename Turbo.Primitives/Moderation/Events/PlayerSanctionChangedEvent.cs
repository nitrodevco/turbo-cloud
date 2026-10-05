using Turbo.Primitives.Events;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Moderation.Events;

/// <summary>
/// Raised when a player is banned from the hotel or their ban is lifted. Not raised when a ban
/// simply runs out. Published without being awaited, once the change is written.
/// </summary>
public sealed record PlayerSanctionChangedEvent : IEvent
{
    public required PlayerId PlayerId { get; init; }
}
