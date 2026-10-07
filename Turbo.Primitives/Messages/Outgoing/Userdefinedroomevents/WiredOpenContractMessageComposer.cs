using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>Tells the client to open a contract editor; it asks for the contents with WiredOpenContractMessage.</summary>
[GenerateSerializer, Immutable]
public sealed record WiredOpenContractMessageComposer : IComposer
{
    [Id(0)]
    public required int ContractId { get; init; }
}
