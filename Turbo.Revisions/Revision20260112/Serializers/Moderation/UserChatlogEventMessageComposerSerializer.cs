using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Moderation;

internal class UserChatlogEventMessageComposerSerializer(int header)
    : AbstractSerializer<UserChatlogEventMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, UserChatlogEventMessageComposer message)
    {
        //
    }
}
