using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;

[GenerateSerializer, Immutable]
public sealed record WiredErrorLogsEventMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<WiredErrorLogSnapshot> Errors { get; init; }
}
