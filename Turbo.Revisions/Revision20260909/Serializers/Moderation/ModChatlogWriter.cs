using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Moderation;

/// <summary>
/// A chatlog record as the client's record parser reads it: the type byte, a short count of
/// typed context entries (key, then type 0 bool / 1 int / 2 string, then the value), and a short
/// count of lines (time, chatter id, chatter name, message, highlighted).
/// </summary>
internal static class ModChatlogWriter
{
    private const byte INT = 1;
    private const byte STRING = 2;

    public static void Write(IServerPacket packet, ModChatlogRecordSnapshot record)
    {
        packet.WriteByte((byte)record.RecordType).WriteShort(2);
        packet.WriteString("roomName").WriteByte(STRING).WriteString(record.RoomName);
        packet.WriteString("roomId").WriteByte(INT).WriteInteger(record.RoomId);
        packet.WriteShort((short)record.Lines.Length);

        foreach (var line in record.Lines)
            packet
                .WriteString(line.Time)
                .WriteInteger(line.ChatterId)
                .WriteString(line.ChatterName)
                .WriteString(line.Message)
                .WriteBoolean(line.Highlighted);
    }
}
