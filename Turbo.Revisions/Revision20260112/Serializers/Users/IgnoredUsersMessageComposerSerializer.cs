using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users;

internal class IgnoredUsersMessageComposerSerializer(int header)
    : AbstractSerializer<IgnoredUsersMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, IgnoredUsersMessageComposer message)
    {
        packet.WriteInteger(message.IgnoredUserIds.Count);

        foreach (var userId in message.IgnoredUserIds)
            packet.WriteInteger(userId);
    }
}
