namespace Turbo.Core.Packets.Messages;

using DotNetty.Buffers;
using DotNetty.Common;

public interface ITurboPacket : IByteBufferHolder
{
    public int Header { get; }
}
