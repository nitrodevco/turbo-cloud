using Turbo.Primitives.Messages.Outgoing.Userclassification;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userclassification;

/// <summary>A count, then each player's id, name and class (<c>UserClassificationMessageEvent</c>).</summary>
internal class UserClassificationMessageComposerSerializer(int header)
    : AbstractSerializer<UserClassificationMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        UserClassificationMessageComposer message
    )
    {
        packet.WriteInteger(message.Users.Length);

        foreach (var user in message.Users)
            packet.WriteInteger(user.UserId).WriteString(user.UserName).WriteString(user.ClassType);
    }
}
