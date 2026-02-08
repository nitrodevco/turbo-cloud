using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine;

internal class UserChangeMessageComposerSerializer(int header)
    : AbstractSerializer<UserChangeMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, UserChangeMessageComposer message)
    {
        packet
            .WriteInteger(message.ObjectId)
            .WriteString(message.Figure)
            .WriteString(message.Gender.ToLegacyString())
            .WriteString(message.CustomInfo)
            .WriteInteger(message.AchievementScore);
    }
}
