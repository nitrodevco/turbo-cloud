using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Handshake;

public record ClientHelloMessage : IMessageEvent
{
    public string Production { get; init; }

    public string Platform { get; init; }

    public int ClientPlatform { get; init; }

    public int DeviceCategory { get; init; }
}
