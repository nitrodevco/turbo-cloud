using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Snapshots.Navigator;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record CompetitionRoomsDataMessageComposer : IComposer
{
    [Id(0)]
    public required CompetitionRoomDataSnapshot RoomData { get; init; }
}
