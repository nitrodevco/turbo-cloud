using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Room.Pets;

[GenerateSerializer, Immutable]
public sealed record PetFigureUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required RoomObjectId ObjectId { get; init; }

    [Id(1)]
    public required int PetId { get; init; }

    [Id(2)]
    public required PetFigureSnapshot Figure { get; init; }

    [Id(3)]
    public required bool HasSaddle { get; init; }

    [Id(4)]
    public required bool IsRiding { get; init; }
}
