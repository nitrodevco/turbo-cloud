namespace Turbo.Primitives.Packets;

public class TurboPacket(int header) : ITurboPacket
{
    public int Header { get; set; } = header;
}
