using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Handshake;

public record UniqueIdMessage : IMessageEvent
{
    public string MachineID { get; init; }
    public string Fingerprint { get; init; }
    public string FlashVersion { get; init; }
}
