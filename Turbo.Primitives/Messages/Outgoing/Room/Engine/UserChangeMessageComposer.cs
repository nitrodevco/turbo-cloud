using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record UserChangeMessageComposer : IComposer
{
    [Id(0)]
    public required RoomObjectId ObjectId { get; init; }

    [Id(1)]
    public required string Figure { get; init; }

    [Id(2)]
    public required AvatarGenderType Gender { get; init; }

    [Id(3)]
    public required string CustomInfo { get; init; }

    [Id(4)]
    public required int AchievementScore { get; init; }
}
