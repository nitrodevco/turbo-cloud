using Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Events.Player;

/// <summary>
/// A player waved, danced, sat down, held up a sign and so on. <see cref="Value"/> says which:
/// the expression, dance, sign or posture, as <see cref="AvatarActionType"/> describes.
/// </summary>
[GenerateSerializer]
public sealed record PlayerPerformsActionEvent : PlayerEvent
{
    [Id(0)]
    public required AvatarActionType ActionType { get; init; }

    [Id(1)]
    public int Value { get; init; }
}
