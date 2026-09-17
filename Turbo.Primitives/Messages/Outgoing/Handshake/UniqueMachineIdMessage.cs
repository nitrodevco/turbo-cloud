using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

[GenerateSerializer, Immutable]
public sealed record UniqueMachineIdMessage : IComposer
{
    [Id(0)]
    public required string MachineID { get; init; }
}
