using DotNetty.Buffers;
using DotNetty.Common;

namespace Turbo.Core.Packets.Messages;

public interface ITurboPacket : IByteBufferHolder
{
    public int Header { get; }
}
