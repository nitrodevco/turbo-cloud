using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots.Wired.VariableFx;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

[GenerateSerializer, Immutable]
public sealed record VariableFxStatusRemovedMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<VariableFxStatusKeySnapshot> Keys { get; init; }
}
