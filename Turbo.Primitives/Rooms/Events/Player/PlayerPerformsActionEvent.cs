using Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Rooms.Events.Player;

/// <summary>
/// A player waved, danced, sat down, held up a sign and so on. <see cref="Value"/> carries the
/// sign or dance id for the actions that have one, otherwise zero.
/// </summary>
[GenerateSerializer]
public sealed record PlayerPerformsActionEvent : PlayerEvent
{
    [Id(0)]
    public required WiredAvatarActionType ActionType { get; init; }

    [Id(1)]
    public int Value { get; init; }
}
