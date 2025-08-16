namespace Turbo.Core.Packets.Messages;

using DotNetty.Buffers;

public interface ISerializer
{
    public int Header { get; }

    public IServerPacket Serialize(IByteBuffer output, IComposer message);
}
