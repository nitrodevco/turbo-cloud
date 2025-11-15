using System.Text;

namespace Turbo.Primitives.Packets;

public interface IClientPacket : ITurboPacket
{
    public int Remaining { get; }
    public bool End { get; }
    public byte PopByte();
    public byte[] PopBytes(int count);
    public bool PopBoolean();
    public short PopShort();
    public ushort PopUShort();
    public int PopInt();
    public long PopLong();
    public string PopString(Encoding? encoding = null);
}
