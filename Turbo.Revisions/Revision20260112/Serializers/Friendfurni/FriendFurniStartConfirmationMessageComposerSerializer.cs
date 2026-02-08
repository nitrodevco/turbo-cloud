using Turbo.Primitives.Messages.Outgoing.Friendfurni;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Friendfurni;

internal class FriendFurniStartConfirmationMessageComposerSerializer(int header)
    : AbstractSerializer<FriendFurniStartConfirmationMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        FriendFurniStartConfirmationMessageComposer message
    )
    {
        //
    }
}
