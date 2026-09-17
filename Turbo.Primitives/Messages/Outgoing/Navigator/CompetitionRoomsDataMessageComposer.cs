using Orleans;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record CompetitionRoomsDataMessageComposer : IComposer
{
    [Id(0)]
    public required CompetitionRoomDataSnapshot RoomData { get; init; }
}
