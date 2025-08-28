using Turbo.Core.Packets.Messages;
using Turbo.Packets.Outgoing;

namespace Turbo.Packets.Serializers;

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
