using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots.Wired.VariableFx;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

[GenerateSerializer, Immutable]
public sealed record VariableFxStatusMessageComposer : IComposer
{
    /// <summary>Marks every status in the message as an initial sync, whatever each one says.</summary>
    [Id(0)]
    public required bool InitializeAll { get; init; }

    [Id(1)]
    public required ImmutableArray<VariableFxStatusSnapshot> Statuses { get; init; }
}
