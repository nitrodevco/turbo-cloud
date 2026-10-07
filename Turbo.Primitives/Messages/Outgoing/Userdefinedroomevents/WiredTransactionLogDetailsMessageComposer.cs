using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>One wired transaction in full.</summary>
[GenerateSerializer, Immutable]
public sealed record WiredTransactionLogDetailsMessageComposer : IComposer
{
    [Id(0)]
    public required WiredTransactionDetailsSnapshot Details { get; init; }
}
