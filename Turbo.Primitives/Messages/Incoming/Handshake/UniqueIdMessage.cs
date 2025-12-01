using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Handshake;

public record UniqueIdMessage : IMessageEvent
{
    public required string MachineID { get; init; }
    public required string Fingerprint { get; init; }
    public required string FlashVersion { get; init; }
}
