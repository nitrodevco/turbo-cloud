using Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Snapshots.Avatars;

[GenerateSerializer, Immutable]
public sealed record RoomRentableBotAvatarSnapshot : RoomAvatarSnapshot
{
    [Id(12)]
    public required AvatarGenderType Gender { get; init; }

    [Id(13)]
    public required int OwnerId { get; init; }

    [Id(14)]
    public required int OwnerName { get; init; }

    [Id(15)]
    public required short[] BotSkills { get; init; }
}
