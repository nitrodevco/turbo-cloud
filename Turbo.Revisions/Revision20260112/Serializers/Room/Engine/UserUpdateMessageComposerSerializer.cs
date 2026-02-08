using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine;

internal class UserUpdateMessageComposerSerializer(int header)
    : AbstractSerializer<UserUpdateMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, UserUpdateMessageComposer message)
    {
        packet.WriteInteger(message.Avatars.Length);

        foreach (var avatar in message.Avatars)
        {
            packet
                .WriteInteger(avatar.ObjectId)
                .WriteInteger(avatar.X)
                .WriteInteger(avatar.Y)
                .WriteString(avatar.Z.ToString())
                .WriteInteger((int)avatar.HeadRotation)
                .WriteInteger((int)avatar.BodyRotation)
                .WriteString(avatar.Status);
        }
    }
}
