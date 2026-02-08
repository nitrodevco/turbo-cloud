using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Avatareffect.Data;

internal class AvatarEffectSerializer
{
    public static void Serialize(IServerPacket packet, AvatarEffectSnapshot effect)
    {
        packet
            .WriteInteger(effect.Type)
            .WriteInteger(effect.SubType)
            .WriteInteger(effect.Duration)
            .WriteInteger(effect.InactiveEffectsInInventory)
            .WriteInteger(effect.SecondsLeftIfActive)
            .WriteBoolean(effect.IsPermanent);
    }
}
