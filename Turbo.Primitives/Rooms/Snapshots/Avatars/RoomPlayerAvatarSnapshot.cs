using Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Snapshots.Avatars;

[GenerateSerializer, Immutable]
public sealed record RoomPlayerAvatarSnapshot : RoomAvatarSnapshot
{
    [Id(12)]
    public required AvatarGenderType Gender { get; init; }

    [Id(13)]
    public required AvatarDanceType DanceType { get; init; }

    [Id(14)]
    public required int GroupId { get; init; }

    [Id(15)]
    public required int GroupStatus { get; init; }

    [Id(16)]
    public required string GroupName { get; init; }

    [Id(17)]
    public required string SwimFigure { get; init; }

    [Id(18)]
    public required int ActivityPoints { get; init; }

    [Id(19)]
    public required bool IsModerator { get; init; }
}
