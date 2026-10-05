using Turbo.Primitives.Events;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Players.Events;

/// <summary>
/// Raised when a player's temporary look is set, replaced or cleared, including when it is
/// cleared because the player disconnected. Not raised for a call that changes nothing.
/// Published without being awaited, so a handler may call back into the player's grain.
/// </summary>
public sealed record PlayerLookOverrideChangedEvent : IEvent
{
    public required PlayerId PlayerId { get; init; }

    /// <summary>The override before the change; null when there was none.</summary>
    public PlayerLookOverrideSnapshot? Previous { get; init; }

    /// <summary>The override now; null when it was cleared.</summary>
    public PlayerLookOverrideSnapshot? Current { get; init; }

    /// <summary>The figure the player is shown with after the change.</summary>
    public required string Figure { get; init; }

    /// <summary>The gender the player is shown with after the change.</summary>
    public required AvatarGenderType Gender { get; init; }
}
