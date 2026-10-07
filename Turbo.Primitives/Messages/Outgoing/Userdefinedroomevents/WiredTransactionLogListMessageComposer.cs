using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>A page of a chest's or a room's wired transaction log.</summary>
[GenerateSerializer, Immutable]
public sealed record WiredTransactionLogListMessageComposer : IComposer
{
    [Id(0)]
    public required WiredTransactionLogListSnapshot LogList { get; init; }
}
