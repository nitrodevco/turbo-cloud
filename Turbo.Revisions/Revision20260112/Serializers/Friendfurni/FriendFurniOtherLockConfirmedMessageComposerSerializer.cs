using Turbo.Primitives.Messages.Outgoing.Friendfurni;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Friendfurni;

internal class FriendFurniOtherLockConfirmedMessageComposerSerializer(int header)
    : AbstractSerializer<FriendFurniOtherLockConfirmedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        FriendFurniOtherLockConfirmedMessageComposer message
    )
    {
        //
    }
}
