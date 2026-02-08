using Turbo.Primitives.Messages.Outgoing.Inventory.Avatareffect;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Avatareffect;

internal class AvatarEffectAddedMessageComposerSerializer(int header)
    : AbstractSerializer<AvatarEffectAddedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        AvatarEffectAddedMessageComposer message
    )
    {
        //
    }
}
