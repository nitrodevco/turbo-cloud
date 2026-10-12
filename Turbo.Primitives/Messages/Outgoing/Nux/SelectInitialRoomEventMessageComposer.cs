using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Messages.Outgoing.Nux;

/// <summary>
/// The answer to a new player's starter room pick: status 0 and the room made for them, which the
/// client then sets as home; any other status (and room 0) ends the flow without a room.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record SelectInitialRoomEventMessageComposer : IComposer
{
    [Id(0)]
    public required short Status { get; init; }

    [Id(1)]
    public required RoomId RoomId { get; init; }
}
