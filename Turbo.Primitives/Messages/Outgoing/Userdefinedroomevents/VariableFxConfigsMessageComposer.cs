using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots.Wired.VariableFx;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>Adds or replaces variable fx configs; statuses refer to them by id, so they go first.</summary>
[GenerateSerializer, Immutable]
public sealed record VariableFxConfigsMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<VariableFxConfigSnapshot> Configs { get; init; }
}
