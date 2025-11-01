using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Snapshots.Navigator;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public record CompetitionRoomsDataMessageComposer : IComposer
{
    public required CompetitionRoomDataSnapshot RoomData { get; init; }
}
