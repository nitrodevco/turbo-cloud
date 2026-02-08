using Turbo.Primitives.Messages.Outgoing.Inventory.Avatareffect;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Avatareffect;

internal class AvatarEffectSelectedMessageComposerSerializer(int header)
    : AbstractSerializer<AvatarEffectSelectedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        AvatarEffectSelectedMessageComposer message
    )
    {
        //
    }
}
