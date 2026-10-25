using Turbo.Primitives.Events;

namespace Turbo.Primitives.Players.Events;

/// <summary>A new player account was made and saved (the site's registration, the panel's).</summary>
public sealed record PlayerRegisteredEvent : DomainEvent
{
    public required PlayerId PlayerId { get; init; }

    public required string Name { get; init; }
}
