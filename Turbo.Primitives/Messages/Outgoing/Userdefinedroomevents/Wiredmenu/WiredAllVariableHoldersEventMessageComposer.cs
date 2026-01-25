using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;

[GenerateSerializer, Immutable]
public sealed record WiredAllVariableHoldersEventMessageComposer : IComposer
{
    [Id(0)]
    public required WiredVariableSnapshot VariableSnapshot { get; init; }

    [Id(1)]
    public required List<(RoomObjectId objectId, int value)> ObjectValues { get; init; }
}
