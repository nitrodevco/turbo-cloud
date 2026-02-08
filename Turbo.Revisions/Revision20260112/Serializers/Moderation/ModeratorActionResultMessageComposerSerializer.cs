using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Moderation;

internal class ModeratorActionResultMessageComposerSerializer(int header)
    : AbstractSerializer<ModeratorActionResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ModeratorActionResultMessageComposer message
    )
    {
        //
    }
}
