using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Room.Action;

[GenerateSerializer, Immutable]
public sealed record AvatarEffectMessageComposer : IComposer
{
    [Id(0)]
    public required RoomObjectId ObjectId { get; init; }

    [Id(1)]
    public required int EffectId { get; init; }

    [Id(2)]
    public required int DelayMilliseconds { get; init; }
}
