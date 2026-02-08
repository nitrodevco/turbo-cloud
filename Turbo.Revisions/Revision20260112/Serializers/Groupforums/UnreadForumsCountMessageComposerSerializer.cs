using Turbo.Primitives.Messages.Outgoing.Groupforums;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Groupforums;

internal class UnreadForumsCountMessageComposerSerializer(int header)
    : AbstractSerializer<UnreadForumsCountMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        UnreadForumsCountMessageComposer message
    )
    {
        packet.WriteInteger(message.UnreadForumsCount);
    }
}
