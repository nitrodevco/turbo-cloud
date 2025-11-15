using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Packets;

public abstract class AbstractSerializer<T>(int header) : ISerializer
    where T : IComposer
{
    public int Header { get; } = header;

    public IServerPacket Serialize(IComposer message)
    {
        var packet = new ServerPacket(Header);

        packet.WriteInteger(0);
        packet.WriteShort((short)Header);

        Serialize(packet, (T)message);

        var length = packet.Length;

        packet.SetWriterPosition(0);
        packet.WriteInteger(length - 4);
        packet.SetWriterPosition(length);

        return packet;
    }

    protected abstract void Serialize(IServerPacket packet, T message);
}
