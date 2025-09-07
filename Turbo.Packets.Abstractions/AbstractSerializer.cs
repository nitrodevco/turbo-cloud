using Turbo.Contracts.Abstractions;

namespace Turbo.Packets.Abstractions;

public abstract class AbstractSerializer<T>(int header) : ISerializer
    where T : IComposer
{
    public int Header { get; } = header;

    public IServerPacket Serialize(IComposer message)
    {
        IServerPacket packet = new ServerPacket(Header);

        packet.WriteShort((short)Header);

        Serialize(packet, (T)message);

        return packet;
    }

    protected abstract void Serialize(IServerPacket packet, T message);
}
