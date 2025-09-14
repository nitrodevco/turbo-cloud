using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Handshake;

public record ClientHelloMessage : IMessageEvent
{
    public required string Production { get; init; }

    public required string Platform { get; init; }

    public int ClientPlatform { get; init; }

    public int DeviceCategory { get; init; }
}
