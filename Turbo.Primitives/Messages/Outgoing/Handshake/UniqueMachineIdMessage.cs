using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

public sealed record UniqueMachineIdMessage : IComposer
{
    public required string MachineID { get; init; }
}
