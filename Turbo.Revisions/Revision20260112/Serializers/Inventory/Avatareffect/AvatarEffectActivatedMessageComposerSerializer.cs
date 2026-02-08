using Turbo.Primitives.Messages.Outgoing.Inventory.Avatareffect;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Avatareffect;

internal class AvatarEffectActivatedMessageComposerSerializer(int header)
    : AbstractSerializer<AvatarEffectActivatedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        AvatarEffectActivatedMessageComposer message
    )
    {
        //
    }
}
