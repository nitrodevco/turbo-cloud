namespace Turbo.Packets.Incoming.Handshake;

using Turbo.Core.Packets.Messages;

public record ClientHelloMessage : IMessageEvent
{
    public string Production { get; init; }

    public string Platform { get; init; }

    public int ClientPlatform { get; init; }

    public int DeviceCategory { get; init; }
}
