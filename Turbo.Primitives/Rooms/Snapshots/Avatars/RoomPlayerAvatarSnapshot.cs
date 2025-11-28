using Orleans;
using Turbo.Contracts.Enums.Rooms.Object;

namespace Turbo.Primitives.Rooms.Snapshots.Avatars;

[GenerateSerializer, Immutable]
public sealed record RoomPlayerAvatarSnapshot : RoomAvatarSnapshot
{
    [Id(0)]
    public required AvatarGenderEnum Gender { get; init; }

    [Id(1)]
    public required int GroupId { get; init; }

    [Id(2)]
    public required int GroupStatus { get; init; }

    [Id(3)]
    public required string GroupName { get; init; }

    [Id(4)]
    public required string SwimFigure { get; init; }

    [Id(5)]
    public required int ActivityPoints { get; init; }

    [Id(6)]
    public required bool IsModerator { get; init; }
}
