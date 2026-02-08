using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users;

internal class RelationshipStatusInfoEventMessageComposerSerializer(int header)
    : AbstractSerializer<RelationshipStatusInfoEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        RelationshipStatusInfoEventMessageComposer message
    )
    {
        //
    }
}
