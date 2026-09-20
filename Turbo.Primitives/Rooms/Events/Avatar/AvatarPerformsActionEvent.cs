using Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Events.Avatar;

/// <summary>
/// An avatar waved, danced, sat down, held up a sign and so on. <see cref="Value"/> says which:
/// the expression, dance, sign or posture, as <see cref="AvatarActionType"/> describes. It names
/// the avatar by its room index, so a bot or a pet reports what it did exactly as a player does.
/// </summary>
[GenerateSerializer]
public sealed record AvatarPerformsActionEvent : AvatarEvent
{
    [Id(0)]
    public required AvatarActionType ActionType { get; init; }

    [Id(1)]
    public int Value { get; init; }
}
