using System.IO;

namespace Turbo.Packets.Abstractions;

public interface IServerPacket : ITurboPacket
{
    public BinaryWriter Writer { get; }
    public MemoryStream Stream { get; }
    public int Length { get; }

    IServerPacket WriteByte(byte b);

    IServerPacket WriteBoolean(bool b);

    IServerPacket WriteShort(short s);

    IServerPacket WriteDouble(double d);

    IServerPacket WriteLong(long l);
    IServerPacket WriteInteger(int i);

    IServerPacket WriteString(string s);
    IServerPacket SetWriterPosition(int position);
    public byte[] ToArray();
}
