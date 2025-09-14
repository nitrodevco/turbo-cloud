using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

public record UniqueMachineIdMessage : IComposer
{
    public required string MachineID { get; init; }
}
