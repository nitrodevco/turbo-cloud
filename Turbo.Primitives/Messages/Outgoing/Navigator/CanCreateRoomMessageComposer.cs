using Orleans;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record CanCreateRoomMessageComposer : IComposer
{
    [Id(0)]
    public required RoomCreationResultType Result { get; init; }

    [Id(1)]
    public int RoomLimit { get; init; }
}
