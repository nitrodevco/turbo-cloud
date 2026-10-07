using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>A contract's contents, which open the editor for its type.</summary>
[GenerateSerializer, Immutable]
public sealed record WiredContractContentsMessageComposer : IComposer
{
    [Id(0)]
    public required WiredContractSnapshot Contract { get; init; }
}
