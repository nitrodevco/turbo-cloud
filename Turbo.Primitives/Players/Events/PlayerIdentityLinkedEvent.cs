using Turbo.Primitives.Events;

namespace Turbo.Primitives.Players.Events;

/// <summary>
/// An account elsewhere (Discord) was linked to a player, who signs in with it from now on.
/// <see cref="TakenFrom"/> is the player who had it before when staff moved it.
/// </summary>
public sealed record PlayerIdentityLinkedEvent : DomainEvent
{
    public required PlayerId PlayerId { get; init; }

    /// <summary>The service (<c>discord</c>).</summary>
    public required string Provider { get; init; }

    /// <summary>The account's id there.</summary>
    public required string IdentityId { get; init; }

    public PlayerId? TakenFrom { get; init; }
}
