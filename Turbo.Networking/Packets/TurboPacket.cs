using System.Text;
using DotNetty.Buffers;

namespace Turbo.Networking.Packets;

public class TurboPacket(int header, IByteBuffer body) : DefaultByteBufferHolder(body)
{
    protected readonly StringBuilder _log = new();

    public int Header { get; set; } = header;

    public override string ToString()
    {
        return _log.ToString();
    }
}