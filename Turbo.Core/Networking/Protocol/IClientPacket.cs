using System.Text;

namespace Turbo.Core.Networking.Protocol;

public interface IClientPacket
{
    public int Header { get; }
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
