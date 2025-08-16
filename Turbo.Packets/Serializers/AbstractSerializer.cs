namespace Turbo.Packets.Serializers;

using DotNetty.Buffers;

using Turbo.Core.Packets.Messages;
using Turbo.Packets.Outgoing;

public abstract class AbstractSerializer<T>(int header) : ISerializer
    where T : IComposer
{
    public int Header { get; } = header;

    public IServerPacket Serialize(IByteBuffer output, IComposer message)
    {
        IServerPacket packet = new ServerPacket(Header, output);

        packet.WriteShort(Header);

        Serialize(packet, (T)message);

        return packet;
    }

    protected abstract void Serialize(IServerPacket packet, T message);
}
