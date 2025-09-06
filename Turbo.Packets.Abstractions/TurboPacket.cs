using System.Text;

namespace Turbo.Packets.Abstractions;

public class TurboPacket(int header) : ITurboPacket
{
    protected readonly StringBuilder _log = new();

    public int Header { get; set; } = header;

    public override string ToString()
    {
        return _log.ToString();
    }
}
