using System;
using System.Text;
using Turbo.Core.Packets.Messages;

namespace Turbo.Packets;

public class TurboPacket(int header) : ITurboPacket
{
    protected readonly StringBuilder _log = new();

    public int Header { get; set; } = header;

    public override string ToString()
    {
        return _log.ToString();
    }
}
