using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Room.Action;

[GenerateSerializer, Immutable]
public sealed record DanceMessageComposer : IComposer
{
    [Id(0)]
    public required int ObjectId { get; init; }

    [Id(1)]
    public required AvatarDanceType DanceType { get; init; }
}
