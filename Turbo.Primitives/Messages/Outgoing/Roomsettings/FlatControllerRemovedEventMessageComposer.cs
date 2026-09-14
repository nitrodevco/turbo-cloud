using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Messages.Outgoing.Roomsettings;

[GenerateSerializer, Immutable]
public sealed record FlatControllerRemovedEventMessageComposer : IComposer
{
    [Id(0)]
    public required RoomId RoomId { get; init; }

    [Id(1)]
    public required PlayerId PlayerId { get; init; }
}
