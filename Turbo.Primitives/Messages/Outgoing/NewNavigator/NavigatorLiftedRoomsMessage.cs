using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.NewNavigator;

[GenerateSerializer, Immutable]
public sealed record NavigatorLiftedRoomsMessage : IComposer
{
    [Id(0)]
    public required List<NavigatorLiftedRoomSnapshot> LiftedRooms { get; init; }
}
