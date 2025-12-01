using Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Snapshots.Avatars;

[GenerateSerializer, Immutable]
public sealed record RoomRentableBotAvatarSnapshot : RoomAvatarSnapshot
{
    [Id(0)]
    public required AvatarGenderEnum Gender { get; init; }

    [Id(1)]
    public required int OwnerId { get; init; }

    [Id(2)]
    public required int OwnerName { get; init; }

    [Id(3)]
    public required short[] BotSkills { get; init; }
}
