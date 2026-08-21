using Turbo.Primitives.Messages.Outgoing.Sound;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Sound;

internal class UserSongDisksInventoryMessageComposerSerializer(int header)
    : AbstractSerializer<UserSongDisksInventoryMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        UserSongDisksInventoryMessageComposer message
    )
    {
        //
    }
}
