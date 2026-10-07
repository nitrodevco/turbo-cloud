using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>A wired transaction completed.</summary>
[GenerateSerializer, Immutable]
public sealed record WiredTransactionSuccessMessageComposer : IComposer
{
    [Id(0)]
    public required WiredTransactionSuccessContentsSnapshot Contents { get; init; }
}
