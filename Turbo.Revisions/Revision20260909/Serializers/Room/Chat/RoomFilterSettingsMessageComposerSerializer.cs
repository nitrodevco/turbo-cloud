using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Chat;

internal class RoomFilterSettingsMessageComposerSerializer(int header)
    : AbstractSerializer<RoomFilterSettingsMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        RoomFilterSettingsMessageComposer message
    )
    {
        packet.WriteInteger(message.BadWords.Count);

        foreach (var word in message.BadWords)
            packet.WriteString(word);
    }
}
