using System.Collections.Generic;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Snapshots.NewNavigator;

namespace Turbo.Primitives.Messages.Outgoing.NewNavigator;

public sealed record NavigatorLiftedRoomsMessage : IComposer
{
    public required List<NavigatorLiftedRoomSnapshot> LiftedRooms { get; init; }
}
