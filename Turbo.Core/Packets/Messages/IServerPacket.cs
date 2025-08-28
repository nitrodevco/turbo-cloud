using System;
using System.IO;

namespace Turbo.Core.Packets.Messages;

public interface IServerPacket : ITurboPacket
{
    public MemoryStream Stream { get; }
    public BinaryWriter Writer { get; }
    public int Length { get; }

    IServerPacket WriteByte(byte b);

    IServerPacket WriteBoolean(bool b);

    IServerPacket WriteShort(short s);

    IServerPacket WriteDouble(double d);

    IServerPacket WriteLong(long l);
    IServerPacket WriteInteger(int i);

    IServerPacket WriteString(string s);
    public byte[] ToArray();
}
