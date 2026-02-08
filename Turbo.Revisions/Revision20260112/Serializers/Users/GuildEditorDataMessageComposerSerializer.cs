using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users;

internal class GuildEditorDataMessageComposerSerializer(int header)
    : AbstractSerializer<GuildEditorDataMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, GuildEditorDataMessageComposer message)
    {
        //
    }
}
