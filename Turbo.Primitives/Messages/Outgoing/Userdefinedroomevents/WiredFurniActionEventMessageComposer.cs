using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

[GenerateSerializer, Immutable]
public sealed record WiredFurniActionEventMessageComposer : IComposer
{
    [Id(0)]
    public required WiredDataSnapshot WiredData { get; init; }
}
