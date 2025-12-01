using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record CanCreateRoomMessageComposer : IComposer
{
    [Id(0)]
    public int ResultCode { get; init; }

    [Id(1)]
    public int RoomLimit { get; init; }
}
