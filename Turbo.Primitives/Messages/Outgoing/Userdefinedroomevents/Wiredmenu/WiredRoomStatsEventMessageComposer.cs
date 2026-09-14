using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;

[GenerateSerializer, Immutable]
public sealed record WiredRoomStatsEventMessageComposer : IComposer
{
    [Id(0)]
    public required WiredRoomStatsSnapshot Stats { get; init; }
}
