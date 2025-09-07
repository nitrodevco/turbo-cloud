using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

public record UniqueMachineIdMessage : IComposer
{
    public string MachineID { get; init; }
}
