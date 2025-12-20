using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record ObjectRemoveMessageComposer : IComposer
{
    [Id(0)]
    public required RoomObjectId ObjectId { get; init; }

    [Id(1)]
    public required bool IsExpired { get; init; }

    [Id(2)]
    public required PlayerId PickerId { get; init; }

    [Id(3)]
    public required int Delay { get; init; }
}
