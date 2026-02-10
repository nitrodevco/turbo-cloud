using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users;

internal class RelationshipStatusInfoEventMessageComposerSerializer(int header)
    : AbstractSerializer<RelationshipStatusInfoEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        RelationshipStatusInfoEventMessageComposer message
    )
    {
        packet.WriteInteger(message.UserId);
        packet.WriteInteger(message.Entries.Count);
        foreach (var entry in message.Entries)
        {
            packet.WriteInteger(entry.RelationshipStatusType);
            packet.WriteInteger(entry.FriendCount);
            packet.WriteInteger(entry.RandomFriendId);
            packet.WriteString(entry.RandomFriendName);
            packet.WriteString(entry.RandomFriendFigure);
        }
    }
}
