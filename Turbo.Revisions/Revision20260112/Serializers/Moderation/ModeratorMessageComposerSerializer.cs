using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Moderation;

internal class ModeratorMessageComposerSerializer(int header)
    : AbstractSerializer<ModeratorMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ModeratorMessageComposer message)
    {
        //
    }
}
