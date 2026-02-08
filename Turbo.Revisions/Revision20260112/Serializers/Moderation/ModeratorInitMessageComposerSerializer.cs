using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Moderation;

internal class ModeratorInitMessageComposerSerializer(int header)
    : AbstractSerializer<ModeratorInitMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ModeratorInitMessageComposer message)
    {
        //
    }
}
