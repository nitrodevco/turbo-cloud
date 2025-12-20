using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record NavigatorSettingsMessageComposer : IComposer
{
    [Id(0)]
    public RoomId HomeRoomId { get; init; }

    [Id(1)]
    public RoomId RoomIdToEnter { get; init; }
}
