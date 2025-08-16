namespace Turbo.Packets;

using System.Text;

using DotNetty.Buffers;
using Turbo.Core.Packets.Messages;

public class TurboPacket(int header, IByteBuffer body) : DefaultByteBufferHolder(body), ITurboPacket
{
    protected readonly StringBuilder _log = new();

    public int Header { get; set; } = header;

    public override string ToString()
    {
        return _log.ToString();
    }
}
